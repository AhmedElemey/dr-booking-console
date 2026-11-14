using System.Net;
using System.Text;
using System.Text.Json;

namespace Console.Tests;

// Stateful in-memory GitHub: records every call and behaves like the REST endpoints the provisioner uses.
public sealed class FakeGitHub : HttpMessageHandler
{
    private readonly object _gate = new();

    public List<string> Calls { get; } = [];

    public Dictionary<string, string> Bodies { get; } = [];

    // repo name -> path -> (sha, content)
    public Dictionary<string, Dictionary<string, (string Sha, string Content)>> Repos { get; } = [];

    public Dictionary<string, Dictionary<string, string>> Variables { get; } = [];

    public Func<HttpRequestMessage, bool>? FailWhen { get; set; }

    // Number of contents lookups that answer 404 right after a repo is generated (templates copy asynchronously).
    public int NotReadyPolls { get; set; }

    // Template repositories (looked up by name under any owner): default branch and whether clinic.config.json is on it.
    public Dictionary<string, (string DefaultBranch, bool HasConfig)> Templates { get; } = new()
    {
        ["dr-booking-backend"] = ("main", true),
        ["dr-booking-dashboard"] = ("main", true),
        ["dr-booking-mobile"] = ("main", true),
        ["dr-booking-website"] = ("main", true),
    };

    private readonly Dictionary<string, int> _polls = [];

    public string? AuthorizationSeen { get; private set; }

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var body = request.Content is null ? "" : await request.Content.ReadAsStringAsync(cancellationToken);
        var path = request.RequestUri!.AbsolutePath.TrimStart('/');
        lock (_gate)
        {
            AuthorizationSeen = request.Headers.Authorization?.ToString();
            var call = $"{request.Method} {path}";
            Calls.Add(call);
            Bodies[call + "#" + Calls.Count(c => c == call)] = body;

            if (FailWhen?.Invoke(request) == true)
            {
                return Json(HttpStatusCode.Forbidden, new { message = "Resource not accessible by personal access token" });
            }

            var parts = path.Split('/');
            if (request.Method == HttpMethod.Post && parts is ["repos", _, _, "generate"])
            {
                var name = JsonDocument.Parse(body).RootElement.GetProperty("name").GetString()!;
                if (Repos.ContainsKey(name))
                {
                    return Json(HttpStatusCode.UnprocessableEntity, new { message = "Repository creation failed.", errors = new[] { new { message = "name already exists on this account" } } });
                }

                Repos[name] = new() { ["clinic.config.json"] = ("sha-template", "{}\n") };
                _polls[name] = 0;
                return Json(HttpStatusCode.Created, new { full_name = "acme/" + name, html_url = "https://github.com/acme/" + name });
            }

            if (parts is ["repos", _, var template, ..] && Templates.TryGetValue(template, out var tpl) && request.Method == HttpMethod.Get)
            {
                return parts switch
                {
                    [_, _, _] => Json(HttpStatusCode.OK, new { default_branch = tpl.DefaultBranch, html_url = "https://github.com/templates/" + template }),
                    [_, _, _, "contents", "clinic.config.json"] when tpl.HasConfig => Json(HttpStatusCode.OK, new { sha = "sha-template", content = Convert.ToBase64String("{}\n"u8.ToArray()), encoding = "base64" }),
                    _ => Json(HttpStatusCode.NotFound, new { message = "Not Found" }),
                };
            }

            if (parts is ["repos", _, var repo, ..] && Repos.TryGetValue(repo, out var files))
            {
                if (parts.Length == 3 && request.Method == HttpMethod.Get)
                {
                    return Json(HttpStatusCode.OK, new { html_url = "https://github.com/acme/" + repo });
                }

                if (parts is [_, _, _, "contents", .. var file])
                {
                    var filePath = string.Join('/', file);
                    if (request.Method == HttpMethod.Get)
                    {
                        if (filePath == "clinic.config.json" && Poll(repo) < NotReadyPolls)
                        {
                            return Json(HttpStatusCode.NotFound, new { message = "Not Found" });
                        }

                        return files.TryGetValue(filePath, out var f)
                            ? Json(HttpStatusCode.OK, new { sha = f.Sha, content = Convert.ToBase64String(Encoding.UTF8.GetBytes(f.Content)), encoding = "base64" })
                            : Json(HttpStatusCode.NotFound, new { message = "Not Found" });
                    }

                    if (request.Method == HttpMethod.Put)
                    {
                        var doc = JsonDocument.Parse(body).RootElement;
                        var existing = files.TryGetValue(filePath, out var cur);
                        if (existing && (!doc.TryGetProperty("sha", out var sha) || sha.GetString() != cur.Sha))
                        {
                            return Json(HttpStatusCode.UnprocessableEntity, new { message = "sha wasn't supplied" });
                        }

                        files[filePath] = ("sha-" + Guid.NewGuid().ToString("N")[..8], Encoding.UTF8.GetString(Convert.FromBase64String(doc.GetProperty("content").GetString()!)));
                        return Json(existing ? HttpStatusCode.OK : HttpStatusCode.Created, new { });
                    }
                }

                if (parts is [_, _, _, "actions", "variables", .. var rest])
                {
                    var vars = Variables.TryGetValue(repo, out var v) ? v : Variables[repo] = [];
                    var payload = body.Length > 0 ? JsonDocument.Parse(body).RootElement : default;
                    if (request.Method == HttpMethod.Post)
                    {
                        var name = payload.GetProperty("name").GetString()!;
                        if (vars.ContainsKey(name))
                        {
                            return Json(HttpStatusCode.Conflict, new { message = "Already exists" });
                        }

                        vars[name] = payload.GetProperty("value").GetString()!;
                        return Json(HttpStatusCode.Created, new { });
                    }

                    if (request.Method.Method == "PATCH" && rest.Length == 1)
                    {
                        vars[rest[0]] = payload.GetProperty("value").GetString()!;
                        return new HttpResponseMessage(HttpStatusCode.NoContent);
                    }
                }
            }

            return Json(HttpStatusCode.NotFound, new { message = "Not Found" });
        }
    }

    private int Poll(string repo)
    {
        var seen = _polls.GetValueOrDefault(repo);
        _polls[repo] = seen + 1;
        return seen;
    }

    private static HttpResponseMessage Json(HttpStatusCode status, object body) =>
        new(status) { Content = new StringContent(JsonSerializer.Serialize(body), Encoding.UTF8, "application/json") };
}
