using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace GolfSG.Api.Controllers;

[ApiController]
public sealed class AdminPageController : ControllerBase
{
    [HttpGet("/admin")]
    [AllowAnonymous]
    public ContentResult Index()
    {
        var nonce = Convert.ToBase64String(System.Security.Cryptography.RandomNumberGenerator.GetBytes(32));
        Response.Headers.CacheControl = "no-store";
        Response.Headers["X-Content-Type-Options"] = "nosniff";
        Response.Headers["Referrer-Policy"] = "no-referrer";
        Response.Headers["Content-Security-Policy"] = $"default-src 'none'; script-src 'nonce-{nonce}'; style-src 'nonce-{nonce}'; connect-src 'self'; frame-ancestors 'none'; base-uri 'none'; form-action 'self'";
        var html = """
            <!doctype html><html lang="en"><meta charset="utf-8">
            <meta name="viewport" content="width=device-width,initial-scale=1">
            <title>GolfSG administration</title>
            <style nonce="ADMIN_NONCE">
            body{font:16px system-ui;background:#f4f2e8;color:#243b30;max-width:900px;margin:40px auto;padding:16px}
            input,button{font:inherit;padding:10px;margin:5px;border:1px solid #526c5c;border-radius:6px}
            button{cursor:pointer;background:white}table{width:100%;border-collapse:collapse;background:white}
            td,th{text-align:left;padding:12px;border-bottom:1px solid #ddd}#status{min-height:24px}
            </style>
            <h1>GolfSG administration</h1>
            <form id="login"><label>Username <input id="username" autocomplete="username" required></label>
            <label>Password <input id="password" type="password" autocomplete="current-password" required></label>
            <button>Sign in</button></form>
            <p id="status" role="status" aria-live="polite"></p>
            <section id="panel" hidden><button id="logout">Sign out</button>
            <h2>Users and account access</h2>
            <p>Disabling access preserves rounds and game records. Revoking sessions requires a fresh login.</p>
            <table><thead><tr><th>User</th><th>Access</th><th>Actions</th></tr></thead><tbody id="rows"></tbody></table>
            <button id="previous">Previous</button><button id="next">Next</button></section>
            <script nonce="ADMIN_NONCE">
            const el=id=>document.getElementById(id);let token='',offset=0;
            function message(text){el('status').textContent=text;}
            function signOut(){token='';el('panel').hidden=true;el('login').hidden=false;el('rows').replaceChildren();}
            async function request(path,options={}){
              const response=await fetch(path,{...options,headers:{...options.headers,Authorization:'Bearer '+token}});
              if(response.status===401){signOut();throw Error('Please sign in again.');}
              if(response.status===403){signOut();throw Error('Super-admin access is required.');}
              if(!response.ok)throw Error(response.status===409?'This account cannot be changed, or the update conflicted.':'Request failed. Please try again.');
              return response.status===204?null:response.json();
            }
            async function load(){
              const users=await request('/api/admin/users?offset='+offset+'&limit=50');el('rows').replaceChildren();
              for(const user of users){
                const row=document.createElement('tr');
                for(const value of [user.username,user.locked?'Locked':'Enabled']){const cell=document.createElement('td');cell.textContent=value;row.append(cell);}
                const actions=document.createElement('td');
                for(const [label,action] of [[user.locked?'Enable':'Disable',user.locked?'enable':'disable'],['Revoke sessions','revoke-sessions']]){
                  const button=document.createElement('button');button.textContent=label;
                  button.onclick=async()=>{
                    if(!confirm(label+' for '+user.username+'?'))return;
                    button.disabled=true;
                    try{await request('/api/admin/users/'+encodeURIComponent(user.id)+'/'+action,{method:'POST'});await load();message('Account updated.');}
                    catch(error){message(error.message);}finally{button.disabled=false;}
                  };actions.append(button);
                }row.append(actions);el('rows').append(row);
              }el('previous').disabled=offset===0;el('next').disabled=users.length<50;
            }
            el('login').onsubmit=async event=>{
              event.preventDefault();message('Signing in…');
              try{
                const response=await fetch('/api/auth/admin-login',{method:'POST',headers:{'Content-Type':'application/json'},
                  body:JSON.stringify({username:el('username').value,password:el('password').value})});
                el('password').value='';if(!response.ok)throw Error('Sign-in failed. Check your credentials or try again later.');
                token=(await response.json()).accessToken;offset=0;await load();el('login').hidden=true;el('panel').hidden=false;message('Signed in.');
              }catch(error){signOut();message(error.message);}
            };
            el('logout').onclick=()=>{signOut();message('Signed out.');};
            el('previous').onclick=async()=>{offset=Math.max(0,offset-50);try{await load();}catch(error){message(error.message);}};
            el('next').onclick=async()=>{offset+=50;try{await load();}catch(error){message(error.message);}};
            </script></html>
            """;
        return Content(html.Replace("ADMIN_NONCE", nonce, StringComparison.Ordinal), "text/html; charset=utf-8");
    }
}
