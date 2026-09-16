// Topos.Samples.TradingLayers — loads week 1387 arrow events into a real Topos HypergraphKernel,
// models each degree as a layer (composable view), and emits an HTML visualization where each
// degree-layer's visibility is toggleable. The data is queried from the Topos kernel itself.
//
// Vertex model:
//   Each PricePoint pivot = one vertex, with typed properties (price, time, degree, direction, glid).
//
// Layer model:
//   Each degree D0–D11 = a Mask (FilteredView) over the kernel: "all vertices whose Degree == D".
//   High band = vertices in that layer with Direction == Down (High pivots), connected in time order.
//   Low band  = vertices in that layer with Direction == Up   (Low pivots),  connected in time order.
//
// This is a design-phase proof: it proves the Topos kernel can hold the trading structure and that
// the degree-as-layer view pattern works. It is NOT the live actor-aware engine (that comes later).
using System.Globalization;
using System.Text.Json;
using Topos.Hypergraph;

namespace Topos.Samples.TradingLayers;

public static class Program
{
    public static int Main(string[] args)
    {
        var dataPath = args.Length > 0
            ? args[0]
            : "/tmp/topos_arrival.json";

        if (!File.Exists(dataPath))
        {
            Console.Error.WriteLine($"Data file not found: {dataPath}");
            Console.Error.WriteLine("Expected format: JSON with 'nodes' array from the arrow-event parser.");
            return 1;
        }

        // ── 1. Load + dedup the arrow events ──────────────────────────────────
        var json = File.ReadAllText(dataPath);
        var doc = JsonDocument.Parse(json);
        var rawNodes = doc.RootElement.GetProperty("nodes").EnumerateArray().ToList();

        // Each raw node: [id, degree, dir, price, recv_label, glid, ptype, arrow_time]
        // Dedup by (glid, degree) — keep first arrival
        var seen = new HashSet<(string glid, int degree)>();
        var pivots = new List<Pivot>();
        foreach (var node in rawNodes)
        {
            var degree = node[1].GetInt32();
            var dir = node[2].GetString()!;
            var price = node[3].GetDouble();
            var glid = node[5].GetString()!;
            var ptype = node[6].GetString()!;
            var arrowTimeStr = node[7].GetString()!;
            var key = (glid, degree);
            if (seen.Add(key))
            {
                pivots.Add(new Pivot(degree, dir, price, glid, ptype, arrowTimeStr));
            }
        }

        Console.WriteLine($"Loaded {rawNodes.Count} raw events → {pivots.Count} unique pivots");

        // ── 2. Build the Topos HypergraphKernel ───────────────────────────────
        var kernel = new HypergraphKernel();

        // Resolve typed properties for pivot vertices
        var pPrice = kernel.ResolveProperty<double>("price");
        var pDegree = kernel.ResolveProperty<int>("degree");
        var pDirection = kernel.ResolveProperty<string>("direction");
        var pGlid = kernel.ResolveProperty<string>("glid");
        var pEpoch = kernel.ResolveProperty<double>("epoch");

        // Create a vertex per pivot, set its properties
        var epochRef = new DateTime(2026, 8, 2, 0, 0, 0, DateTimeKind.Utc).Subtract(
            DateTime.UnixEpoch).TotalSeconds;

        foreach (var pv in pivots)
        {
            var h = kernel.CreateVertex();
            kernel.SetProperty(pPrice, h, pv.Price);
            kernel.SetProperty(pDegree, h, pv.Degree);
            kernel.SetProperty(pDirection, h, pv.Direction);
            kernel.SetProperty(pGlid, h, pv.Glid);
            var epoch = DateTime.ParseExact(pv.ArrowTimeStr, "yyyyMMdd HH:mm:ss",
                          CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal)
                       .Subtract(DateTime.UnixEpoch).TotalSeconds;
            kernel.SetProperty(pEpoch, h, epoch);
        }

        Console.WriteLine($"Kernel: {kernel.VertexHandles().Count} vertices");

        // ── 3. Build per-degree layer views (Mask = FilteredView) ─────────────
        // Each degree layer = "all vertices where degree == D"
        // This demonstrates the Topos composable-view pattern (USAGE_PATTERNS Pattern 4a).
        var degreeColors = new (int r, int g, int b)[]
        {
            (255,0,0),(255,165,0),(255,255,0),(0,128,0),(0,255,255),(0,0,255),
            (128,0,128),(255,0,255),(255,192,203),(165,42,42),(128,128,128),(101,67,33)
        };

        // Query the kernel for each degree's pivots, sorted by epoch
        // (In a real actor-aware engine, the views are live; here we query once for the static viz)
        var layers = new Dictionary<int, List<(double epoch, double price, string dir, string glid)>>();
        for (var d = 0; d < 12; d++)
        {
            var layerView = HypergraphViews.Mask(kernel, h =>
            {
                if (!kernel.TryGetProperty(pDegree, h, out var deg)) return false;
                return deg == d;
            });

            var pts = new List<(double epoch, double price, string dir, string glid)>();
            // Enumerate vertices in the layer view
            foreach (var h in layerView.VertexHandles())
            {
                if (!layerView.TryGetVertex(h, out var v)) continue;
                kernel.TryGetProperty(pEpoch, h, out var epoch);
                kernel.TryGetProperty(pPrice, h, out var price);
                kernel.TryGetProperty(pDirection, h, out var dir);
                kernel.TryGetProperty(pGlid, h, out var glid);
                pts.Add((epoch, price, dir!, glid!));
            }
            pts.Sort((a, b) => a.epoch.CompareTo(b.epoch));
            layers[d] = pts;
            if (pts.Count > 0)
                Console.WriteLine($"  D{d} layer: {pts.Count} vertices (highs={pts.Count(p=>p.dir=="Down")}, lows={pts.Count(p=>p.dir=="Up")})");
        }

        // ── 4. Emit the HTML visualization ───────────────────────────────────
        // Each degree's high-band and low-band lines, with toggleable layer visibility.
        var html = BuildHtml(layers, degreeColors);
        var outPath = "/Users/nassertowfigh/Projects/TradingSystem/docs/Topos_LayerStack_Week1387.html";
        File.WriteAllText(outPath, html);
        Console.WriteLine($"\nWrote {outPath} ({new FileInfo(outPath).Length:N0} bytes)");
        Console.WriteLine("Open with: open " + outPath);
        return 0;
    }

    private static string BuildHtml(
        Dictionary<int, List<(double epoch, double price, string dir, string glid)>> layers,
        (int r, int g, int b)[] colors)
    {
        // Compute global ranges
        var allEpochs = layers.Values.SelectMany(l => l).Select(p => p.epoch).ToList();
        var allPrices = layers.Values.SelectMany(l => l).Select(p => p.price).ToList();
        var tMin = allEpochs.Min() - 3600;
        var tMax = allEpochs.Max() + 3600;
        var pMin = allPrices.Min() - 50;
        var pMax = allPrices.Max() + 50;

        // Build per-degree high/low line arrays as JSON
        var sb = new System.Text.StringBuilder();
        sb.Append("const HL={");
        for (var d = 0; d < 12; d++)
        {
            if (d > 0) sb.Append(',');
            var highs = layers.GetValueOrDefault(d, new())
                .Where(p => p.dir == "Down")
                .Select(p => $"[{p.epoch:F1},{p.price:F1}]");
            sb.Append($"\"{d}\":[{string.Join(',', highs)}]");
        }
        sb.Append("};\nconst LL={");
        for (var d = 0; d < 12; d++)
        {
            if (d > 0) sb.Append(',');
            var lows = layers.GetValueOrDefault(d, new())
                .Where(p => p.dir == "Up")
                .Select(p => $"[{p.epoch:F1},{p.price:F1}]");
            sb.Append($"\"{d}\":[{string.Join(',', lows)}]");
        }
        sb.Append($"}};\n");

        var colorsJson = $"[{string.Join(',', colors.Select(c => $"[{c.r},{c.g},{c.b}]"))}]";

        // Build legend items
        var legendItems = string.Join('\n', colors.Select((c, d) =>
            $@"<div class=""li""><span class=""ld"" style=""background:rgb({c.r},{c.g},{c.b})""></span>D{d}</div>"));

        return $@"<!DOCTYPE html>
<html lang=""en""><head><meta charset=""UTF-8"">
<title>Topos Layers — High/Low Bands per Degree (Week 1387)</title>
<style>
html,body{{margin:0;padding:0;height:100%;background:#0a0a0a;color:#ddd;font-family:-apple-system,system-ui,sans-serif;overflow:hidden}}
#wrap{{display:flex;flex-direction:column;height:100vh}}
#header{{padding:8px 14px;background:#141414;border-bottom:1px solid #222}}
#header h1{{margin:0;font-size:14px;font-weight:600}}
#header .sub{{font-size:11px;color:#777;margin-top:2px}}
#header .topos-badge{{display:inline-block;background:#1a3a1a;color:#4f4;padding:1px 6px;border-radius:3px;font-size:10px;margin-left:8px}}
#main{{flex:1;position:relative;overflow:hidden}}
canvas{{display:block;width:100%;height:100%}}
#controls{{padding:7px 14px;background:#141414;border-top:1px solid #222;display:flex;align-items:center;gap:10px;font-size:12px}}
button{{background:#222;color:#ddd;border:1px solid #333;border-radius:3px;padding:4px 11px;cursor:pointer;font-size:12px}}
button:hover{{background:#333}}
input[type=range]{{flex:1}}
#legend{{display:flex;gap:6px;flex-wrap:wrap;padding:5px 14px;background:#101010;border-top:1px solid #1a1a1a;font-size:10px}}
.li{{display:flex;align-items:center;gap:3px}}
.ld{{width:9px;height:9px;border-radius:50%}}
#stats{{position:absolute;top:8px;left:8px;background:rgba(12,12,12,.9);border:1px solid #333;border-radius:5px;padding:5px 9px;font-size:11px;line-height:1.5}}
#filters{{position:absolute;top:8px;right:8px;background:rgba(12,12,12,.93);border:1px solid #333;border-radius:5px;padding:6px 10px;font-size:11px}}
.k{{color:#777}}
</style></head><body>
<div id=""wrap"">
<div id=""header"">
<h1>Topos Hypergraph — Degree Layers <span class=""topos-badge"">Live kernel data</span></h1>
<div class=""sub"">Each degree D0–D11 = a Topos Mask (layer view) over the HypergraphKernel · High band connects Highs in sequence · Low band connects Lows · toggle layers to isolate · data queried from the real Topos kernel</div>
</div>
<div id=""main"">
<canvas id=""c""></canvas>
<div id=""stats"">Vertices shown: <span id=""nv"">0</span></div>
<div id=""filters"">
<div class=""k"">Degree layers:</div>
<div id=""deg-toggles"" style=""display:flex;flex-wrap:wrap;gap:3px;margin-top:4px;max-width:220px""></div>
<div style=""margin-top:6px"">
<label><input type=""checkbox"" id=""show-high"" checked> High bands</label><br>
<label><input type=""checkbox"" id=""show-low"" checked> Low bands</label><br>
<label><input type=""checkbox"" id=""show-dots""> Pivot dots</label>
</div>
</div>
</div>
<div id=""legend"">{legendItems}</div>
<div id=""controls"">
<button id=""play"">▶ Play</button>
<button id=""reset"">↺ Reset</button>
<span>Progress:</span>
<input type=""range"" id=""scrub"" min=""0"" max=""100"" value=""100"">
<span id=""tl"" style=""min-width:60px"">100%</span>
<span style=""margin-left:auto;color:#777"">Speed:</span>
<select id=""speed""><option value=""1"">1×</option><option value=""5"">5×</option><option value=""15"" selected>15×</option><option value=""50"">50×</option></select>
</div>
</div>
<script>
{sb}
const DC={colorsJson};
var T_MIN={tMin:F1},T_MAX={tMax:F1},P_MIN={pMin:F1},P_MAX={pMax:F1};
var TR=T_MAX-T_MIN||1,PR=P_MAX-P_MIN||1;

const cv=document.getElementById('c'),ctx=cv.getContext('2d');
let W=0,H=0;
function resize(){{W=cv.clientWidth;H=cv.clientHeight;cv.width=W*devicePixelRatio;cv.height=H*devicePixelRatio;ctx.setTransform(devicePixelRatio,0,0,devicePixelRatio,0,0);}}
addEventListener('resize',()=>{{resize();draw();}});

var ML=55,MR=30,MT=15,MB=15;
function eX(t){{return ML+((t-T_MIN)/TR)*(W-ML-MR);}}
function eY(p){{return MT+(1-(p-P_MIN)/PR)*(H-MT-MB);}}

var visibleD=new Set([0,1,2,3,4,5,6,7,8,9,10,11]);
var dt=document.getElementById('deg-toggles');
for(var d=0;d<12;d++){{var c=DC[d];var lbl=document.createElement('label');
  lbl.style.fontSize='10px';lbl.style.cursor='pointer';
  lbl.innerHTML='<input type=""checkbox"" checked data-deg=""'+d+'"" style=""margin:0""> <span style=""color:rgb('+c[0]+','+c[1]+','+c[2]+')"">D'+d+'</span>';
  dt.appendChild(lbl);}}
dt.querySelectorAll('input').forEach(function(cb){{
  cb.onchange=function(){{var d=+cb.dataset.deg;cb.checked?visibleD.add(d):visibleD.delete(d);draw();}};}});

var showHigh=document.getElementById('show-high');
var showLow=document.getElementById('show-low');
var showDots=document.getElementById('show-dots');

var progress=1.0,playing=false,last=0,spd=15;
var scrub=document.getElementById('scrub'),pb=document.getElementById('play');
var nv=document.getElementById('nv'),tl=document.getElementById('tl');
document.getElementById('reset').onclick=function(){{progress=0;playing=false;pb.textContent='▶ Play';scrub.value=0;draw();}};
pb.onclick=function(){{if(progress>=1)progress=0;playing=!playing;pb.textContent=playing?'⏸ Pause':'▶ Play';last=performance.now();}};
scrub.oninput=function(){{progress=+scrub.value/100;draw();}};
document.getElementById('speed').onchange=function(e){{spd=+e.target.value;}};

function drawLine(pts,deg,curTime){{
  if(!pts||pts.length<1)return 0;
  var c=DC[deg];
  ctx.strokeStyle='rgba('+c[0]+','+c[1]+','+c[2]+',0.7)';
  ctx.lineWidth=Math.max(1.0,1.5+deg*0.15);
  ctx.beginPath();
  var started=false,count=0;
  for(var i=0;i<pts.length;i++){{
    if(pts[i][0]>curTime)break;
    var x=eX(pts[i][0]),y=eY(pts[i][1]);
    if(!started){{ctx.moveTo(x,y);started=true;}}
    else ctx.lineTo(x,y);
    count++;
  }}
  if(started)ctx.stroke();
  return count;
}}

function draw(){{
  if(W===0||H===0)return;
  ctx.clearRect(0,0,W,H);
  ctx.strokeStyle='#1a1a1a';ctx.lineWidth=1;ctx.fillStyle='#333';ctx.font='9px sans-serif';ctx.textAlign='right';
  for(var f=0;f<=5;f++){{var gy=MT+(f/5)*(H-MT-MB);
    ctx.beginPath();ctx.moveTo(ML,gy);ctx.lineTo(W-MR,gy);ctx.stroke();
    ctx.fillText((P_MAX-(f/5)*PR).toFixed(0),ML-4,gy+3);}}

  var curTime=T_MIN+progress*(T_MAX-T_MIN);
  var shown=0;

  if(showHigh.checked){{
    for(var d=0;d<12;d++){{if(!visibleD.has(d))continue;
      shown+=drawLine(HL[String(d)],d,curTime);}}
  }}
  if(showLow.checked){{
    for(var d=0;d<12;d++){{if(!visibleD.has(d))continue;
      shown+=drawLine(LL[String(d)],d,curTime);}}
  }}
  if(showDots.checked){{
    for(var d=0;d<12;d++){{
      if(!visibleD.has(d))continue;
      var hPts=HL[String(d)]||[],lPts=LL[String(d)]||[];
      var all=hPts.concat(lPts);
      for(var i=0;i<all.length;i++){{
        if(all[i][0]>curTime)break;
        var c=DC[d];var x=eX(all[i][0]),y=eY(all[i][1]);
        var r=Math.max(2,2+d*0.3);
        ctx.fillStyle='rgba('+c[0]+','+c[1]+','+c[2]+',0.8)';
        ctx.beginPath();ctx.arc(x,y,r,0,7);ctx.fill();
      }}
    }}
  }}
  nv.textContent=shown;
  tl.textContent=(progress*100).toFixed(0)+'%';
}}
function tick(now){{
  if(playing){{
    progress=Math.min(1,progress+(now-last)/1000*spd*0.01);
    if(progress>=1){{playing=false;pb.textContent='▶ Play';}}
    last=now;scrub.value=progress*100;draw();
  }}
  requestAnimationFrame(tick);
}}
resize();draw();requestAnimationFrame(tick);
</script></body></html>";
    }

    private record Pivot(int Degree, string Direction, double Price, string Glid, string Ptype, string ArrowTimeStr);
}
