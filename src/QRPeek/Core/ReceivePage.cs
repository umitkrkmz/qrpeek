// QRPeek - read QR codes on your screen, create QR codes
// Copyright (C) 2026 QRPeek
// SPDX-License-Identifier: GPL-3.0-or-later

using System.Net;
using System.Text;

namespace QRPeek.Core
{
    /// <summary>
    /// The page the phone opens for "Receive from phone". One self-contained
    /// file: no external scripts, fonts or images, no cookies (the device key
    /// lives in sessionStorage for this tab only).
    ///
    /// Flow: POST hello (get key + match code) → pick files → POST offer (list
    /// only) → poll status until the PC accepts or rejects → PUT each file.
    /// </summary>
    internal static class ReceivePage
    {
        public static string Build()
        {
            string Esc(string t) => WebUtility.HtmlEncode(t);
            var t = new StringBuilder("{");
            void Add(string k, string v) => t.Append(t.Length > 1 ? "," : "").Append(k).Append(':').Append(LanHttp.JsonString(v));
            Add("pick", S.PgPick);
            Add("pickMore", S.PgPickMore);
            Add("waiting", S.PgWaiting);
            Add("sending", S.PgSending);
            Add("done", S.PgDone);
            Add("rejected", S.PgRejected);
            Add("busy", S.PgBusy);
            Add("closed", S.PgClosed);
            Add("failed", S.PgFailed);
            Add("limit", S.PgLimit);
            Add("connecting", S.PgConnecting);
            t.Append('}');

            return "<!doctype html><html lang=\"" + (S.IsTurkish ? "tr" : "en") + "\"><head><meta charset=\"utf-8\">"
                   + "<meta name=\"viewport\" content=\"width=device-width,initial-scale=1\"><title>QRPeek</title>"
                   + "<style>"
                   + ":root{color-scheme:light dark}body{margin:0;font:16px/1.45 system-ui,-apple-system,Segoe UI,Roboto,sans-serif;background:#f6f7f9;color:#1b1f24}"
                   + "@media(prefers-color-scheme:dark){body{background:#1b1d21;color:#eceef1}.card{background:#25282d!important;border-color:#3a3f46!important}.bar{background:#3a3f46!important}}"
                   + "main{max-width:520px;margin:0 auto;padding:28px 16px}h1{font-size:20px;margin:0 0 6px}"
                   + ".intro{margin:0 0 18px;opacity:.7;font-size:14px}"
                   + ".card{background:#fff;border:1px solid #dce0e6;border-radius:14px;padding:16px;margin-bottom:14px}"
                   + ".code{display:none;align-items:baseline;gap:10px}.code b{font-size:30px;letter-spacing:6px;font-variant-numeric:tabular-nums}.code span{opacity:.7;font-size:14px}"
                   + "#msg{font-weight:600;min-height:1.45em}"
                   + ".btn{display:none;text-align:center;background:#2563eb;color:#fff;border-radius:10px;padding:14px 18px;font-weight:600;font-size:17px;cursor:pointer}"
                   + ".bar{height:6px;border-radius:3px;background:#dce0e6;overflow:hidden;margin-top:12px;display:none}.bar div{height:100%;width:0;background:#2563eb;transition:width .2s}"
                   + "ul{margin:10px 0 0;padding-left:18px;font-size:14px;opacity:.8;word-break:break-all}"
                   + "</style></head><body><main>"
                   + "<h1>QRPeek</h1><p class=\"intro\">" + Esc(S.PgIntro) + "</p>"
                   + "<div class=\"card code\" id=\"code\"><span>" + Esc(S.PgCode) + "</span><b id=\"codeNum\"></b></div>"
                   + "<div class=\"card\" id=\"status\"><div id=\"msg\"></div><ul id=\"list\"></ul><div class=\"bar\" id=\"bar\"><div id=\"fill\"></div></div></div>"
                   + "<label class=\"btn\" id=\"pickBtn\"><span id=\"pickText\"></span><input type=\"file\" id=\"pick\" multiple hidden></label>"
                   + "</main><script>"
                   + "var T=" + t + ";"
                   + Script
                   + "</script></body></html>";
        }

        // Plain ES5-ish JavaScript so older phone browsers work too.
        private const string Script = @"
var base=location.pathname.replace(/\/?$/,'/'),key=sessionStorage.getItem('qrpeek:'+base)||'';
function $(id){return document.getElementById(id)}
function say(t){$('msg').textContent=t;$('status').style.display=t?'block':'none'}
function showPick(more){$('pickText').textContent=more?T.pickMore:T.pick;$('pickBtn').style.display='block'}
function hidePick(){$('pickBtn').style.display='none'}
function bar(p){$('bar').style.display=p==null?'none':'block';$('fill').style.width=(p||0)+'%'}
function req(method,path,body){return fetch(base+path,{method:method,headers:{'X-QRPeek-Key':key},body:body,cache:'no-store'})}
function hello(){
  say(T.connecting);
  req('POST','hello').then(function(r){
    if(r.status==409){say(T.busy);return}
    if(!r.ok){say(T.closed);return}
    return r.json().then(function(j){
      key=j.key;sessionStorage.setItem('qrpeek:'+base,key);
      $('codeNum').textContent=j.code;$('code').style.display='flex';
      say('');showPick(false);
    });
  }).catch(function(){say(T.closed)});
}
$('pick').addEventListener('change',function(){
  var files=Array.prototype.slice.call(this.files);this.value='';
  if(!files.length)return;
  hidePick();bar(null);
  var ul=$('list');ul.innerHTML='';
  files.forEach(function(f){var li=document.createElement('li');li.textContent=f.name;ul.appendChild(li)});
  var body=files.map(function(f){return f.size+' '+encodeURIComponent(f.name)}).join('\n');
  req('POST','offer',body).then(function(r){
    if(r.status==413){say(T.limit);showPick(true);return}
    if(!r.ok){say(r.status==410?T.closed:T.failed);if(r.status!=410)showPick(true);return}
    return r.json().then(function(j){say(T.waiting);wait(j.offer,files)});
  }).catch(function(){say(T.closed)});
});
function wait(id,files){
  setTimeout(function(){
    req('GET','status?offer='+encodeURIComponent(id)).then(function(r){
      if(!r.ok){say(T.closed);return}
      return r.json().then(function(j){
        if(j.state=='pending')wait(id,files);
        else if(j.state=='accepted')send(id,files);
        else{say(j.state=='rejected'?T.rejected:T.failed);$('list').innerHTML='';showPick(true)}
      });
    }).catch(function(){say(T.closed)});
  },1000);
}
function send(id,files){
  var total=files.reduce(function(a,f){return a+f.size},0)||1,done=0,i=0;
  say(T.sending);bar(0);
  (function next(){
    if(i>=files.length){say(T.done);bar(100);$('list').innerHTML='';showPick(true);return}
    var f=files[i],x=new XMLHttpRequest();
    x.open('PUT',base+'file?offer='+encodeURIComponent(id)+'&i='+i);
    x.setRequestHeader('X-QRPeek-Key',key);
    x.upload.onprogress=function(e){bar(Math.floor((done+e.loaded)*100/total))};
    x.onload=function(){if(x.status==200){done+=f.size;i++;next()}else{say(T.failed);bar(null);showPick(true)}};
    x.onerror=function(){say(T.failed);bar(null);showPick(true)};
    x.send(f);
  })();
}
hello();
";
    }
}
