// LocalhostStreamServer.cs - Node.js backed LAN + WASM + PWA Streaming Server
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Diagnostics;
using QRCoder;

namespace UnixBrowser.Services
{
    public class LocalhostStreamServer : IDisposable
    {
        private const int PreferredPort = 9731;
        private int _actualPort = PreferredPort;
        private Process? _nodeProcess;
        private bool _isRunning = false;
        private readonly string _serverDir;
        private readonly string _wasmDir;

        public bool IsRunning => _isRunning;
        public string LocalIP  => GetLocalIP();
        public string StreamUrl => $"http://{LocalIP}:{_actualPort}";
        public string QrPayload => StreamUrl;

        public event Action<string>? OnStatusChanged;
        public event Action<byte[]>? OnDataReceived;
        public event Action<string>? OnFileRequested;

        public LocalhostStreamServer()
        {
            _serverDir = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "UnixBrowser", "server");
            _wasmDir = Path.Combine(_serverDir, "wasm");
            Directory.CreateDirectory(_serverDir);
            Directory.CreateDirectory(_wasmDir);
        }

        public async Task StartAsync()
        {
            if (_isRunning) return;
            try
            {
                _actualPort = FindFreePort(PreferredPort);
                ExtractApk();
                WriteNodeServer(_actualPort);
                var nodePath = FindNode();
                var serverJs = Path.Combine(_serverDir, "server.js");

                _nodeProcess = new Process
                {
                    StartInfo = new ProcessStartInfo(nodePath, $"\"{serverJs}\"")
                    {
                        UseShellExecute        = false,
                        CreateNoWindow         = true,
                        RedirectStandardOutput = true,
                        RedirectStandardError  = true,
                        WorkingDirectory       = _serverDir
                    },
                    EnableRaisingEvents = true
                };

                _nodeProcess.OutputDataReceived += (_, e) => { if (e.Data != null) OnStatusChanged?.Invoke(e.Data); };
                _nodeProcess.ErrorDataReceived  += (_, e) => { if (e.Data != null) OnStatusChanged?.Invoke($"[err] {e.Data}"); };
                _nodeProcess.Exited             += (_, _) => { _isRunning = false; OnStatusChanged?.Invoke("Server stopped"); };

                _nodeProcess.Start();
                _nodeProcess.BeginOutputReadLine();
                _nodeProcess.BeginErrorReadLine();

                // Give Node a moment to bind
                await Task.Delay(600);
                _isRunning = true;
                TryAddFirewallRule(_actualPort);
                OnStatusChanged?.Invoke($"Stream server started: {StreamUrl}");
            }
            catch (Exception ex)
            {
                OnStatusChanged?.Invoke($"Server error: {ex.Message}");
            }
        }

        private void WriteNodeServer(int port)
        {
            var localIp = GetLocalIP();
            var qrPayload = $"http://{localIp}:{port}";
            var serverJs = Path.Combine(_serverDir, "server.js");
            var pwaHtml = BuildPwaHtml(qrPayload, port);
            var pwaHtmlEscaped = pwaHtml.Replace("\\", "\\\\").Replace("`", "\\`").Replace("${", "\\${");

            var js = $@"
const http = require('http');
const fs   = require('fs');
const path = require('path');
const url  = require('url');
const os   = require('os');
const PORT  = {port};
const WASM_DIR = {JsonEscape(_wasmDir)};
const LOCAL_IP  = '{localIp}';
const sseClients = [];

const server = http.createServer((req, res) => {{
  const parsed = url.parse(req.url, true);
  const p = parsed.pathname;
  const q = parsed.query;

  res.setHeader('Access-Control-Allow-Origin', '*');
  res.setHeader('Access-Control-Allow-Methods', 'GET,POST,PUT,OPTIONS');
  res.setHeader('Access-Control-Allow-Headers', 'Content-Type');

  if (req.method === 'OPTIONS') {{ res.writeHead(204); res.end(); return; }}

  // SSE
  if (p === '/stream/live') {{
    res.writeHead(200, {{ 'Content-Type': 'text/event-stream', 'Cache-Control': 'no-cache', 'Connection': 'keep-alive' }});
    res.write(': connected\n\n');
    sseClients.push(res);
    req.on('close', () => {{ const i = sseClients.indexOf(res); if (i >= 0) sseClients.splice(i,1); }});
    return;
  }}

  // PWA routes
  if (p === '/pwa' || p === '/pwa/') {{
    res.writeHead(200, {{ 'Content-Type': 'text/html; charset=utf-8' }});
    res.end(`{pwaHtmlEscaped}`);
    return;
  }}
  if (p === '/pwa/manifest.json') {{
    res.writeHead(200, {{ 'Content-Type': 'application/manifest+json' }});
    res.end(JSON.stringify({{
      name: 'Unix Browser', short_name: 'UnixBrowser',
      description: 'Low-level web browser - linux/x86/x64/wasm',
      start_url: '/pwa', display: 'standalone', orientation: 'any',
      background_color: '#0d0d0d', theme_color: '#00ff00',
      icons: [
        {{ src: '/pwa/icon.png', sizes: '192x192', type: 'image/png', purpose: 'any maskable' }},
        {{ src: '/pwa/icon.png', sizes: '512x512', type: 'image/png', purpose: 'any maskable' }}
      ],
      shortcuts: [
        {{ name: 'Stream', url: '/', description: 'Stream dashboard' }},
        {{ name: 'WASM', url: '/wasm/list', description: 'WASM modules' }}
      ]
    }}, null, 2));
    return;
  }}
  if (p === '/pwa/sw.js') {{
    res.writeHead(200, {{ 'Content-Type': 'application/javascript' }});
    res.end(`
const CACHE='unix-browser-v1';
const ASSETS=['/pwa','/pwa/manifest.json'];
self.addEventListener('install',e=>{{e.waitUntil(caches.open(CACHE).then(c=>c.addAll(ASSETS)));self.skipWaiting();}});
self.addEventListener('activate',e=>{{e.waitUntil(caches.keys().then(ks=>Promise.all(ks.filter(k=>k!==CACHE).map(k=>caches.delete(k)))));self.clients.claim();}});
self.addEventListener('fetch',e=>{{if(new URL(e.request.url).pathname.startsWith('/pwa')){{e.respondWith(caches.match(e.request).then(r=>r||fetch(e.request)));}}}}); 
`);
    return;
  }}
  if (p === '/pwa/icon.png') {{
    // 1x1 green PNG
    const icon = Buffer.from('iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVR42mNk+M9QDwADhgGAWjR9awAAAABJRU5ErkJggg==','base64');
    res.writeHead(200, {{ 'Content-Type': 'image/png' }});
    res.end(icon);
    return;
  }}

  // APK download
  if (p === '/apk') {{
    const apkPath = path.join(__dirname, 'UnixBrowser.apk');
    if (fs.existsSync(apkPath)) {{
      const stat = fs.statSync(apkPath);
      res.writeHead(200, {{
        'Content-Type': 'application/vnd.android.package-archive',
        'Content-Disposition': 'attachment; filename=UnixBrowser.apk',
        'Content-Length': stat.size
      }});
      fs.createReadStream(apkPath).pipe(res);
    }} else {{
      res.writeHead(404, {{ 'Content-Type': 'application/json' }});
      res.end(JSON.stringify({{ error: 'APK not found on server' }}));
    }}
    return;
  }}

  // Root dashboard
  if (p === '/' || p === '/index.html') {{
    res.writeHead(200, {{ 'Content-Type': 'text/html; charset=utf-8' }});
    res.end(`<!DOCTYPE html><html><head><meta charset='utf-8'><meta name='viewport' content='width=device-width,initial-scale=1'>
<title>Unix Stream</title>
<style>body{{background:#0d0d0d;color:#00ff00;font-family:Courier New,monospace;padding:20px;max-width:700px;margin:0 auto}}
h1{{text-shadow:0 0 10px #00ff00}}.box{{border:1px solid #00ff00;padding:12px;margin:10px 0;background:#111}}
button{{background:#003300;border:1px solid #00ff00;color:#00ff00;padding:8px 14px;margin:4px;cursor:pointer;font-family:Courier New,monospace}}
button:hover{{background:#00ff00;color:#000}}pre{{background:#0a0a0a;padding:10px;font-size:11px;max-height:180px;overflow-y:auto;white-space:pre-wrap}}
input{{background:#111;border:1px solid #00ff00;color:#00ff00;padding:6px;font-family:Courier New,monospace;width:65%}}</style></head>
<body><h1>&#9889; Unix Stream Server</h1>
<p style='color:#888;font-size:11px'>LAN: <b style='color:#00ccff'>http://${{LOCAL_IP}}:{port}</b> &nbsp;|&nbsp; <a href='/pwa' style='color:#ff6600'>&#128640; PWA</a> &nbsp;|&nbsp; <a href='/apk' style='color:#00ff00'>&#128241; Download APK</a></p>
<div class='box'><h3 style='color:#00ccff'>&#128225; Live Events <span id='dot' style='color:#ff0000'>&#9679;</span></h3><pre id='log'>waiting...</pre></div>
<div class='box'><h3 style='color:#00ccff'>&#129710; WebAssembly</h3>
<input type='file' id='wf' accept='.wasm'> <button onclick='uploadWasm()'>Upload .wasm</button> <button onclick='listWasm()'>List</button>
<pre id='wo'>---</pre></div>
<div class='box'><h3 style='color:#00ccff'>&#128228; Upload File</h3>
<input type='file' id='df'> <button onclick='uploadFile()'>Upload</button><pre id='uo'>---</pre></div>
<div class='box'><h3 style='color:#00ccff'>&#128187; Exec</h3>
<input type='text' id='cmd' value='ipconfig'> <button onclick='runCmd()'>Run</button><pre id='eo'>---</pre></div>
<script>
const es=new EventSource('/stream/live');
es.onopen=()=>{{document.getElementById('dot').style.color='#00ff00';}};
es.onmessage=e=>{{const l=document.getElementById('log');l.textContent='['+new Date().toLocaleTimeString()+'] '+e.data+'\\n'+l.textContent.slice(0,1000);}};
es.onerror=()=>{{document.getElementById('dot').style.color='#ff0000';}};
async function uploadWasm(){{const f=document.getElementById('wf').files[0];if(!f)return;
const r=await fetch('/wasm/upload?name='+encodeURIComponent(f.name),{{method:'POST',body:await f.arrayBuffer()}});
document.getElementById('wo').textContent=JSON.stringify(await r.json(),null,2);}}
async function listWasm(){{const r=await fetch('/wasm/list');document.getElementById('wo').textContent=JSON.stringify(await r.json(),null,2);}}
async function uploadFile(){{const f=document.getElementById('df').files[0];if(!f)return;
const r=await fetch('/upload?name='+encodeURIComponent(f.name),{{method:'POST',body:await f.arrayBuffer()}});document.getElementById('uo').textContent=JSON.stringify(await r.json(),null,2);}}
async function runCmd(){{const r=await fetch('/exec?cmd='+encodeURIComponent(document.getElementById('cmd').value));
const j=await r.json();document.getElementById('eo').textContent=(j.output||'')+(j.error?'\\n[err]'+j.error:'');}}
</script></body></html>`);
    return;
  }}

  // Stream info
  if (p === '/stream') {{
    res.writeHead(200, {{ 'Content-Type': 'application/json' }});
    res.end(JSON.stringify({{ status:'ready', lan:`http://${{LOCAL_IP}}:{port}`, sse:`http://${{LOCAL_IP}}:{port}/stream/live`, wasm:`http://${{LOCAL_IP}}:{port}/wasm/list`, pwa:`http://${{LOCAL_IP}}:{port}/pwa` }}));
    return;
  }}

  // Upload
  if (p === '/upload' && req.method === 'POST') {{
    const chunks = [];
    req.on('data', c => chunks.push(c));
    req.on('end', () => {{
      const buf = Buffer.concat(chunks);
      const rawName = (q.name || '').replace(/[^a-zA-Z0-9._\-()\s]/g, '_');
      const ext = rawName && path.extname(rawName) ? path.extname(rawName) : '.png';
      const base = rawName ? path.basename(rawName, ext) : 'upload';
      const fname = `${{base}}_${{Date.now()}}${{ext}}`;
      const downloadsDir = path.join(os.homedir(), 'Downloads');
      if (!fs.existsSync(downloadsDir)) fs.mkdirSync(downloadsDir, {{ recursive: true }});
      fs.writeFileSync(path.join(downloadsDir, fname), buf);
      broadcast(JSON.stringify({{ event:'upload', file:fname, size:buf.length }}));
      res.writeHead(200, {{ 'Content-Type':'application/json' }});
      res.end(JSON.stringify({{ success:true, file:fname, size:buf.length, savedTo:downloadsDir }}));
    }});
    return;
  }}

  // Data
  if (p === '/data' && req.method === 'POST') {{
    const chunks = [];
    req.on('data', c => chunks.push(c));
    req.on('end', () => {{
      const buf = Buffer.concat(chunks);
      broadcast(JSON.stringify({{ event:'data', size:buf.length }}));
      res.writeHead(200, {{ 'Content-Type':'application/json' }});
      res.end(JSON.stringify({{ received:buf.length }}));
    }});
    return;
  }}

  // WASM list
  if (p === '/wasm/list') {{
    const files = fs.existsSync(WASM_DIR)
      ? fs.readdirSync(WASM_DIR).filter(f=>f.endsWith('.wasm')).map(f=>{{ const st=fs.statSync(path.join(WASM_DIR,f)); return {{ name:f, size:st.size, url:`http://${{LOCAL_IP}}:{port}/wasm/${{f}}` }}; }})
      : [];
    res.writeHead(200, {{ 'Content-Type':'application/json' }});
    res.end(JSON.stringify({{ wasm:files, dir:WASM_DIR }}));
    return;
  }}

  // WASM download
  if (p.startsWith('/wasm/') && req.method === 'GET') {{
    const name = path.basename(p);
    const fpath = path.join(WASM_DIR, name);
    if (fs.existsSync(fpath)) {{
      res.writeHead(200, {{ 'Content-Type':'application/wasm', 'Cross-Origin-Embedder-Policy':'require-corp' }});
      fs.createReadStream(fpath).pipe(res);
    }} else {{
      res.writeHead(404, {{ 'Content-Type':'application/json' }});
      res.end(JSON.stringify({{ error:'not found', name }}));
    }}
    return;
  }}

  // WASM upload
  if (p === '/wasm/upload' && req.method === 'POST') {{
    const name = (q.name || `module_${{Date.now()}}.wasm`).replace(/[^a-zA-Z0-9._-]/g,'_');
    const fname = name.endsWith('.wasm') ? name : name+'.wasm';
    const chunks = [];
    req.on('data', c => chunks.push(c));
    req.on('end', () => {{
      const buf = Buffer.concat(chunks);
      fs.writeFileSync(path.join(WASM_DIR, fname), buf);
      broadcast(JSON.stringify({{ event:'wasm_uploaded', name:fname, size:buf.length }}));
      res.writeHead(200, {{ 'Content-Type':'application/json' }});
      res.end(JSON.stringify({{ success:true, name:fname, size:buf.length, url:`http://${{LOCAL_IP}}:{port}/wasm/${{fname}}` }}));
    }});
    return;
  }}

  // Exec
  if (p === '/exec') {{
    const cmd = q.cmd || '';
    if (!cmd) {{ res.writeHead(400, {{ 'Content-Type':'application/json' }}); res.end(JSON.stringify({{ error:'cmd required' }})); return; }}
    const {{ exec }} = require('child_process');
    exec(cmd, {{ timeout:10000 }}, (err, stdout, stderr) => {{
      broadcast(JSON.stringify({{ event:'exec', cmd }}));
      res.writeHead(200, {{ 'Content-Type':'application/json' }});
      res.end(JSON.stringify({{ cmd, output:stdout, error:stderr, exitCode:err?.code||0 }}));
    }});
    return;
  }}

  res.writeHead(404, {{ 'Content-Type':'text/plain' }});
  res.end('404 Not Found');
}});

function broadcast(msg) {{
  sseClients.forEach(c => {{ try {{ c.write(`data: ${{msg}}\n\n`); }} catch(e) {{}} }});
}}

server.listen(PORT, '0.0.0.0', () => {{
  console.log(`Stream server started: http://${{LOCAL_IP}}:{port}`);
}});
server.on('error', err => {{ console.error('Server error: '+err.message); }});
";
            File.WriteAllText(serverJs, js);
        }

        private void ExtractApk()
        {
            var apkDest = Path.Combine(_serverDir, "UnixBrowser.apk");
            if (File.Exists(apkDest)) return; // already extracted
            try
            {
                var asm = System.Reflection.Assembly.GetExecutingAssembly();
                var resName = asm.GetManifestResourceNames()
                    .FirstOrDefault(n => n.EndsWith("UnixBrowser.apk"));
                if (resName == null) return;
                using var stream = asm.GetManifestResourceStream(resName)!;
                using var fs = File.Create(apkDest);
                stream.CopyTo(fs);
            }
            catch { }
        }

        private string BuildPwaHtml(string baseUrl, int port)
        {
            var localIp = GetLocalIP();
            var sb = new StringBuilder();
            sb.Append("<!DOCTYPE html><html lang='en'><head>");
            sb.Append("<meta charset='utf-8'><meta name='viewport' content='width=device-width,initial-scale=1'>");
            sb.Append("<meta name='theme-color' content='#00ff00'>");
            sb.Append("<meta name='apple-mobile-web-app-capable' content='yes'>");
            sb.Append("<meta name='apple-mobile-web-app-status-bar-style' content='black-translucent'>");
            sb.Append("<meta name='apple-mobile-web-app-title' content='Unix Browser'>");
            sb.Append("<link rel='manifest' href='/pwa/manifest.json'>");
            sb.Append("<link rel='apple-touch-icon' href='/pwa/icon.png'>");
            sb.Append("<title>Unix Browser</title>");
            sb.Append("<style>*{box-sizing:border-box;margin:0;padding:0}");
            sb.Append("body{background:#0d0d0d;color:#00ff00;font-family:Courier New,monospace;height:100vh;display:flex;flex-direction:column}");
            sb.Append("#tb{background:#111;border-bottom:1px solid #003300;padding:7px 10px;display:flex;align-items:center;gap:6px;flex-shrink:0}");
            sb.Append("#tb h1{font-size:13px;color:#00ff00;text-shadow:0 0 8px #00ff00;flex:1}");
            sb.Append(".badge{font-size:9px;border:1px solid #00ff00;padding:1px 5px;color:#00cc00}");
            sb.Append("#nav{background:#0a0a0a;border-bottom:1px solid #002200;padding:5px 8px;display:flex;gap:5px;flex-shrink:0;align-items:center}");
            sb.Append("#ub{flex:1;background:#111;border:1px solid #004400;color:#00ccff;padding:5px 8px;font-family:Courier New,monospace;font-size:12px}");
            sb.Append("button{background:#003300;border:1px solid #005500;color:#00ff00;padding:4px 9px;cursor:pointer;font-size:12px;font-family:Courier New,monospace}");
            sb.Append("button:hover{background:#005500}");
            sb.Append("#wv{flex:1;border:none}");
            sb.Append("#sb{background:#050505;border-top:1px solid #001100;padding:3px 8px;font-size:9px;color:#006600;display:flex;justify-content:space-between;flex-shrink:0}");
            sb.Append(".dot{width:6px;height:6px;border-radius:50%;background:#ff0000;display:inline-block;margin-right:3px}");
            sb.Append(".dot.on{background:#00ff00;animation:p 1s infinite}@keyframes p{0%,100%{opacity:1}50%{opacity:0.3}}");
            sb.Append("</style></head><body>");
            sb.Append("<div id='tb'><button onclick='toggleMenu()'>&#9776;</button><h1>&#9889; Unix Browser</h1>");
            sb.Append("<span class='badge'>PWA</span><span class='badge' style='border-color:#ff6600;color:#ff6600'>WASM</span>");
            sb.Append("<span id='ib' style='display:none'><button onclick='installPwa()' style='border-color:#00ccff;color:#00ccff'>&#128640; Install</button></span></div>");
            sb.Append("<div id='nav'><button onclick='history.back()'>&#9664;</button><button onclick='history.forward()'>&#9654;</button>");
            sb.Append("<button onclick='document.getElementById(\"wv\").src=document.getElementById(\"wv\").src'>&#8635;</button>");
            sb.Append("<input id='ub' type='url' value='https://google.com' onkeydown='if(event.key==\"Enter\")go(this.value)' placeholder='Enter URL...'>");
            sb.Append("<button onclick='go(document.getElementById(\"ub\").value)'>Go</button></div>");
            sb.Append("<iframe id='wv' src='https://google.com' style='flex:1;border:none' sandbox='allow-scripts allow-forms allow-same-origin allow-popups' allow='fullscreen'></iframe>");
            sb.Append($"<div id='sb'><span><span class='dot on' id='sdot'></span><a href='http://{localIp}:{port}' style='color:#006600'>Stream: {localIp}:{port}</a></span><span id='pi'>Ready</span></div>");
            sb.Append("<script>");
            sb.Append("let dp=null;window.addEventListener('beforeinstallprompt',e=>{e.preventDefault();dp=e;document.getElementById('ib').style.display='inline';});");
            sb.Append("function installPwa(){if(dp){dp.prompt();dp.userChoice.then(r=>{if(r.outcome==='accepted')document.getElementById('ib').style.display='none';dp=null;});}else{alert('Tap browser menu \\u2192 Add to Home Screen');}}");
            sb.Append("function go(u){if(!u.startsWith('http'))u='https://'+u;document.getElementById('ub').value=u;document.getElementById('wv').src=u;document.getElementById('pi').textContent='Loading...';}");
            sb.Append("function toggleMenu(){alert('Bookmarks: Google | GitHub | MDN\\nStream: "+localIp+":"+port+"');}");
            sb.Append($"try{{const es=new EventSource('http://{localIp}:{port}/stream/live');es.onopen=()=>document.getElementById('sdot').className='dot on';es.onerror=()=>document.getElementById('sdot').className='dot';}}catch(e){{}}");
            sb.Append("if('serviceWorker' in navigator)navigator.serviceWorker.register('/pwa/sw.js');");
            sb.Append("</script></body></html>");
            return sb.ToString();
        }

        private static string JsonEscape(string s) => System.Text.Json.JsonSerializer.Serialize(s);

        private static string FindNode()
        {
            foreach (var candidate in new[] { "node", @"C:\Program Files\nodejs\node.exe", @"C:\Program Files (x86)\nodejs\node.exe" })
            {
                try
                {
                    var p = new Process { StartInfo = new ProcessStartInfo("where", candidate) { UseShellExecute=false, CreateNoWindow=true, RedirectStandardOutput=true } };
                    p.Start(); var out2 = p.StandardOutput.ReadLine(); p.WaitForExit();
                    if (!string.IsNullOrEmpty(out2) && File.Exists(out2.Trim())) return out2.Trim();
                }
                catch { }
                if (File.Exists(candidate)) return candidate;
            }
            return "node";
        }

        private static int FindFreePort(int preferred)
        {
            for (int port = preferred; port < preferred + 100; port++)
            {
                try { var t = new System.Net.Sockets.TcpListener(IPAddress.Any, port); t.Start(); t.Stop(); return port; }
                catch { }
            }
            return preferred;
        }

        private static void TryAddFirewallRule(int port)
        {
            try
            {
                var psi = new ProcessStartInfo("netsh", $"advfirewall firewall add rule name=\"UnixBrowser Stream\" dir=in action=allow protocol=TCP localport={port}")
                { UseShellExecute=false, CreateNoWindow=true };
                Process.Start(psi)?.WaitForExit(3000);
            }
            catch { }
        }

        public string GenerateQrCodeBase64(string payload)
        {
            try
            {
                using var gen  = new QRCodeGenerator();
                using var data = gen.CreateQrCode(payload, QRCodeGenerator.ECCLevel.Q);
                using var code = new PngByteQRCode(data);
                var png = code.GetGraphic(8, new byte[] { 0, 255, 0 }, new byte[] { 13, 13, 13 });
                return Convert.ToBase64String(png);
            }
            catch { return ""; }
        }

        public async Task StopAsync()
        {
            if (!_isRunning) return;
            _isRunning = false;
            try
            {
                if (_nodeProcess != null && !_nodeProcess.HasExited)
                {
                    _nodeProcess.Kill(entireProcessTree: true);
                    await Task.Delay(300);
                }
            }
            catch { }
            OnStatusChanged?.Invoke("Stream server stopped");
        }

        private string GetLocalIP()
        {
            try
            {
                using var socket = new System.Net.Sockets.Socket(System.Net.Sockets.AddressFamily.InterNetwork, System.Net.Sockets.SocketType.Dgram, 0);
                socket.Connect("8.8.8.8", 65432);
                return (socket.LocalEndPoint as IPEndPoint)?.Address.ToString() ?? "127.0.0.1";
            }
            catch { return "127.0.0.1"; }
        }

        public void Dispose()
        {
            StopAsync().Wait(3000);
            _nodeProcess?.Dispose();
        }
    }
}
