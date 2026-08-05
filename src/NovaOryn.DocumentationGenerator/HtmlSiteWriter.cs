using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace NovaOryn.DocumentationGenerator;

internal static class HtmlSiteWriter
{
    internal static void Write(string root, DocumentationConfiguration configuration, IReadOnlyList<ProjectDocumentation> projects)
    {
        string output = Path.GetFullPath(Path.Combine(root, configuration.OutputDirectory));
        if (Directory.Exists(output)) Directory.Delete(output, true);
        Directory.CreateDirectory(output);
        Directory.CreateDirectory(Path.Combine(output, "assets"));
        Directory.CreateDirectory(Path.Combine(output, "assemblies"));
        Directory.CreateDirectory(Path.Combine(output, "api"));
        Directory.CreateDirectory(Path.Combine(output, "guides"));

        File.WriteAllText(Path.Combine(output, "assets", "site.css"), Css);
        File.WriteAllText(Path.Combine(output, "assets", "site.js"), JavaScript);
        foreach (ProjectDocumentation project in projects)
        {
            WriteAssembly(output, configuration, project);
            foreach (ApiDocumentation item in project.Items) WriteApi(output, configuration, item);
        }
        CopyGuides(root, output, configuration);
        WriteIndex(output, configuration, projects);
        WriteSearchIndex(output, projects);
    }

    private static void WriteIndex(string output, DocumentationConfiguration config, IReadOnlyList<ProjectDocumentation> projects)
    {
        string cards = string.Join(Environment.NewLine, projects.Select(project =>
            $"<a class=\"card\" href=\"assemblies/{EncodeFile(project.Name)}.html\"><strong>{H(project.Name)}</strong><span>{project.Items.Count} public items</span></a>"));
        string body = $"<section class=\"hero\"><p class=\"eyebrow\">VERSION {H(config.Version)}</p><h1>Build a freestanding C# kernel.</h1><p>Offline SDK reference generated from the NovaOryn source tree. Browse assemblies, public items, dependencies and usage guidance.</p><div class=\"actions\"><a href=\"guides/Getting-Started.html\">Get started</a><a href=\"guides/Next-Steps.html\">SDK roadmap</a></div></section><section><h2>SDK assemblies</h2><div class=\"cards\">{cards}</div></section>";
        File.WriteAllText(Path.Combine(output, "index.html"), Page(config, "SDK usage", body, string.Empty));
    }

    private static void WriteAssembly(string output, DocumentationConfiguration config, ProjectDocumentation project)
    {
        string items = string.Join(Environment.NewLine, project.Items.Select(item =>
            $"<tr><td><a href=\"../api/{CreateApiFileName(item)}.html\">{H(item.Name)}</a></td><td>{H(item.Kind)}</td><td>{H(item.Summary.Length == 0 ? "Documentation pending." : item.Summary)}</td></tr>"));
        string body = $"<p class=\"eyebrow\">{(project.IsToolAssembly ? "SDK TOOL" : "PUBLIC ASSEMBLY")}</p><h1>{H(project.Name)}</h1><dl><dt>Project</dt><dd>{H(project.ProjectPath)}</dd><dt>Dependencies</dt><dd>{H(project.Dependencies.Count == 0 ? "None" : string.Join(", ", project.Dependencies))}</dd></dl><h2>Public items</h2><table><thead><tr><th>Name</th><th>Kind</th><th>Purpose</th></tr></thead><tbody>{items}</tbody></table>";
        File.WriteAllText(Path.Combine(output, "assemblies", EncodeFile(project.Name) + ".html"), Page(config, project.Name, body, "../"));
    }

    private static void WriteApi(string output, DocumentationConfiguration config, ApiDocumentation item)
    {
        string body = $"<p class=\"eyebrow\">{H(item.Assembly)} · {H(item.Kind)}</p><h1>{H(item.QualifiedName)}</h1><pre><code>{H(item.Signature)}</code></pre>{Section("What it does", item.Summary)}{Section("When to use it", item.WhenToUse)}{Section("Details", item.Remarks)}{Section("Dependencies", item.Dependencies)}{Section("Return value", item.Returns)}{CodeSection("Example", item.Example)}<h2>Source</h2><p><code>{H(item.SourcePath)}:{item.SourceLine}</code></p>";
        File.WriteAllText(Path.Combine(output, "api", CreateApiFileName(item) + ".html"), Page(config, item.Name, body, "../"));
    }

    private static void CopyGuides(string root, string output, DocumentationConfiguration config)
    {
        string content = Path.Combine(root, "docs", "site-content");
        if (!Directory.Exists(content)) return;
        foreach (string markdown in Directory.EnumerateFiles(content, "*.md", SearchOption.TopDirectoryOnly))
        {
            string title = Path.GetFileNameWithoutExtension(markdown).Replace('-', ' ');
            string html = Markdown(File.ReadAllText(markdown));
            File.WriteAllText(Path.Combine(output, "guides", Path.GetFileNameWithoutExtension(markdown) + ".html"), Page(config, title, html, "../"));
        }
    }

    private static void WriteSearchIndex(string output, IReadOnlyList<ProjectDocumentation> projects)
    {
        object[] entries = projects.SelectMany(project => project.Items.Select(item => new
        {
            title = item.QualifiedName,
            assembly = item.Assembly,
            kind = item.Kind,
            summary = item.Summary,
            url = $"api/{CreateApiFileName(item)}.html"
        })).Cast<object>().ToArray();
        File.WriteAllText(Path.Combine(output, "search-index.json"), JsonSerializer.Serialize(entries, new JsonSerializerOptions { WriteIndented = true }));
    }

    private static string Page(DocumentationConfiguration config, string title, string body, string root) => $"""
<!doctype html><html lang="en"><head><meta charset="utf-8"><meta name="viewport" content="width=device-width,initial-scale=1"><title>{H(title)} · {H(config.Product)}</title><link rel="stylesheet" href="{root}assets/site.css"><script defer src="{root}assets/site.js"></script></head><body><header><a class="brand" href="{root}index.html">Nova Oryn <span>OS SDK</span></a><nav><a href="{root}guides/Getting-Started.html">Guides</a><a href="{root}index.html#assemblies">API</a></nav><label class="search"><span>Search</span><input id="site-search" data-root="{root}" placeholder="Type a public item"><div id="search-results"></div></label></header><main>{body}</main><footer>Generated from NovaOryn {H(config.Version)} source.</footer></body></html>
""";

    private static string Section(string title, string value) => $"<h2>{H(title)}</h2><p>{H(value.Length == 0 ? "Documentation pending." : value)}</p>";
    private static string CodeSection(string title, string value) => $"<h2>{H(title)}</h2><pre><code>{H(value.Length == 0 ? "No example has been added yet." : value)}</code></pre>";
    private static string H(string value) => WebUtility.HtmlEncode(value);
    private static string EncodeFile(string value) => Regex.Replace(value.ToLowerInvariant(), "[^a-z0-9]+", "-").Trim('-');

    private static string CreateApiFileName(ApiDocumentation item)
    {
        string readable = EncodeFile(item.Name);
        if (readable.Length == 0) readable = "item";
        if (readable.Length > 48) readable = readable[..48].TrimEnd('-');

        string identity = string.Join("|", item.Assembly, item.Kind, item.QualifiedName, item.Signature);
        byte[] digest = SHA256.HashData(Encoding.UTF8.GetBytes(identity));
        string suffix = Convert.ToHexString(digest.AsSpan(0, 8)).ToLowerInvariant();
        return $"{readable}-{suffix}";
    }

    private static string Markdown(string text)
    {
        StringBuilder html = new();
        bool code = false;
        foreach (string raw in text.Replace("\r\n", "\n").Split('\n'))
        {
            string line = raw.TrimEnd();
            if (line.StartsWith("```", StringComparison.Ordinal)) { html.AppendLine(code ? "</code></pre>" : "<pre><code>"); code = !code; continue; }
            if (code) { html.AppendLine(H(raw)); continue; }
            if (line.StartsWith("### ")) html.Append("<h3>").Append(H(line[4..])).AppendLine("</h3>");
            else if (line.StartsWith("## ")) html.Append("<h2>").Append(H(line[3..])).AppendLine("</h2>");
            else if (line.StartsWith("# ")) html.Append("<h1>").Append(H(line[2..])).AppendLine("</h1>");
            else if (line.StartsWith("- ")) html.Append("<p class=\"bullet\">• ").Append(H(line[2..])).AppendLine("</p>");
            else if (line.Length != 0) html.Append("<p>").Append(H(line)).AppendLine("</p>");
        }
        return html.ToString();
    }

    private const string Css = """
:root{font-family:Inter,Segoe UI,Arial,sans-serif;color:#eaf0ff;background:#07101d;line-height:1.55}*{box-sizing:border-box}body{margin:0}header{position:sticky;top:0;z-index:5;display:flex;align-items:center;gap:2rem;padding:1rem 5vw;background:#07101dee;border-bottom:1px solid #20304a;backdrop-filter:blur(12px)}a{color:#73c7ff;text-decoration:none}.brand{font-weight:800;color:#fff;font-size:1.15rem}.brand span{color:#73c7ff}nav{display:flex;gap:1rem}.search{margin-left:auto;position:relative}.search span{position:absolute;left:-9999px}.search input{width:min(28vw,24rem);padding:.7rem .9rem;border:1px solid #314662;border-radius:.6rem;background:#0d1929;color:#fff}#search-results{position:absolute;right:0;width:32rem;max-width:85vw;background:#0d1929;border:1px solid #314662;border-radius:.6rem;box-shadow:0 1rem 3rem #0008}#search-results a{display:block;padding:.7rem .9rem;border-bottom:1px solid #20304a}main{max-width:76rem;margin:auto;padding:4rem 5vw 7rem}.hero{padding:4rem 0}.hero h1{font-size:clamp(2.6rem,7vw,5.8rem);line-height:.95;max-width:12ch;margin:.2em 0}.hero p{max-width:48rem;font-size:1.2rem;color:#b7c5d9}.eyebrow{letter-spacing:.14em;text-transform:uppercase;color:#73c7ff;font-weight:700}.actions{display:flex;gap:1rem;margin-top:2rem}.actions a,.card{border:1px solid #314662;border-radius:.8rem;padding:1rem 1.2rem;background:#0d1929}.cards{display:grid;grid-template-columns:repeat(auto-fit,minmax(15rem,1fr));gap:1rem}.card{display:flex;flex-direction:column}.card span{color:#9fafc3;font-size:.9rem}h1{font-size:2.7rem}h2{margin-top:2.4rem}pre{overflow:auto;background:#020711;border:1px solid #20304a;padding:1rem;border-radius:.7rem}code{font-family:Cascadia Code,Consolas,monospace}table{width:100%;border-collapse:collapse}th,td{text-align:left;padding:.75rem;border-bottom:1px solid #20304a;vertical-align:top}dt{color:#8da0b9;font-weight:700}dd{margin:0 0 1rem}.bullet{margin:.3rem 0}footer{padding:2rem 5vw;color:#7f90a7;border-top:1px solid #20304a}@media(max-width:700px){header{flex-wrap:wrap}.search{order:3;width:100%}.search input{width:100%}nav{margin-left:auto}}
""";

    private const string JavaScript = """
(async()=>{const input=document.querySelector('#site-search');const box=document.querySelector('#search-results');if(!input||!box)return;const root=input.dataset.root||'';let items=[];try{items=await(await fetch(root+'search-index.json')).json()}catch{return}input.addEventListener('input',()=>{const q=input.value.trim().toLowerCase();box.innerHTML='';if(q.length<2)return;for(const item of items.filter(x=>(x.title+' '+x.assembly+' '+x.summary).toLowerCase().includes(q)).slice(0,8)){const a=document.createElement('a');a.href=root+item.url;a.textContent=item.title+' — '+item.assembly;box.appendChild(a)}})})();
""";
}
