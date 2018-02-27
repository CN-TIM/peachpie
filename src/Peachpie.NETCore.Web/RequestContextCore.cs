﻿using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.Extensions.Primitives;
using Pchp.Core;
using Pchp.Core.Utilities;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Threading;

namespace Peachpie.Web
{
    /// <summary>
    /// Runtime context for ASP.NET Core request.
    /// </summary>
    sealed class RequestContextCore : Context, IHttpPhpContext
    {
        /// <summary>Debug display string.</summary>
        protected override string DebugDisplay => $"{_httpctx.Request.Path.Value}{_httpctx.Request.QueryString.Value}";

        #region IHttpPhpContext

        /// <summary>Gets value indicating HTTP headers were already sent.</summary>
        bool IHttpPhpContext.HeadersSent
        {
            get { return _httpctx.Response.HasStarted; }
        }

        void IHttpPhpContext.SetHeader(string name, string value)
        {
            StringValues newitem = new StringValues(value);
            //StringValues olditem;
            //if (_httpctx.Response.Headers.TryGetValue(name, out olditem))
            //{
            //    newitem = StringValues.Concat(olditem, newitem);
            //}

            //
            _httpctx.Response.Headers[name] = newitem;
        }

        void IHttpPhpContext.RemoveHeader(string name) { _httpctx.Response.Headers.Remove(name); }

        void IHttpPhpContext.RemoveHeaders() { _httpctx.Response.Headers.Clear(); }

        /// <summary>Enumerates HTTP headers in current response.</summary>
        IEnumerable<KeyValuePair<string, string>> IHttpPhpContext.GetHeaders()
        {
            return _httpctx.Response.Headers.Select(pair => new KeyValuePair<string, string>(pair.Key, pair.Value.ToString()));
        }

        public string CacheControl
        {
            get => _httpctx.Response.Headers["cache-control"];
            set => _httpctx.Response.Headers.Add("cache-control", new StringValues(value)); // TODO: set headers properly
        }

        public event Action HeadersSending
        {
            add
            {
                if (_headersSending == null)
                {
                    _httpctx.Response.OnStarting(() =>
                    {
                        _headersSending?.Invoke();
                        return Task.CompletedTask;
                    });
                }

                _headersSending += value;
            }
            remove
            {
                _headersSending -= value;
            }
        }
        Action _headersSending;

        /// <summary>
        /// Gets or sets HTTP response status code.
        /// </summary>
        public int StatusCode
        {
            get { return _httpctx.Response.StatusCode; }
            set { _httpctx.Response.StatusCode = value; }
        }

        /// <summary>
        /// Stream with contents of the incoming HTTP entity body.
        /// </summary>
        Stream IHttpPhpContext.InputStream => _httpctx.Request.Body;

        void IHttpPhpContext.AddCookie(string name, string value, DateTimeOffset? expires, string path, string domain, bool secure, bool httpOnly)
        {
            _httpctx.Response.Cookies.Append(name, value, new CookieOptions()
            {
                Expires = expires,
                Path = path,
                Domain = string.IsNullOrEmpty(domain) ? null : domain,  // IE, Edge: cookie with the empty domain was not passed to request
                Secure = secure,
                HttpOnly = httpOnly
            });
        }

        void IHttpPhpContext.Flush()
        {
            _httpctx.Response.Body.Flush();
        }

        /// <summary>
        /// Gets max request size (upload size, post size) in bytes.
        /// Gets <c>0</c> if limit is not set.
        /// </summary>
        public long MaxRequestSize
        {
            get
            {
                return 30_000_000; // TODO: since 2.0.0: _httpctx.Features.Get<IHttpMaxRequestBodySizeFeature>().MaxRequestBodySize;
            }
        }

        /// <summary>
        /// Gets or sets session handler for current context.
        /// </summary>
        PhpSessionHandler IHttpPhpContext.SessionHandler
        {
            get => _sessionhandler ?? AspNetCoreSessionHandler.Default;
            set
            {
                if (_sessionhandler != null && _sessionhandler != value)
                {
                    _sessionhandler.CloseSession(this, this, abandon: true);
                }

                _sessionhandler = value;
            }
        }
        PhpSessionHandler _sessionhandler;

        /// <summary>
        /// Gets or sets session state.
        /// </summary>
        PhpSessionState IHttpPhpContext.SessionState { get; set; }

        #endregion

        #region Request Lifecycle

        /// <summary>
        /// The default document.
        /// </summary>
        const string DefaultDocument = "index.php";

        public static ScriptInfo ResolveScript(HttpRequest req)
        {
            var script = default(ScriptInfo);
            var path = req.Path.Value;

            var isfile = !path.Last().IsDirectorySeparator();
            if (isfile)
            {
                // path
                script = ScriptsMap.GetDeclaredScript(path);
            }

            if (!script.IsValid)
            {
                // path/defaultdocument
                path = path.TrimEndSeparator();
                path = path.Length != 0 ? (path + ('/' + DefaultDocument)) : DefaultDocument;

                //
                script = ScriptsMap.GetDeclaredScript(path);
            }

            //
            return script;
        }

        /// <summary>
        /// Performs the request lifecycle, invokes given entry script and cleanups the context.
        /// </summary>
        /// <param name="script">Entry script.</param>
        public void ProcessScript(ScriptInfo script)
        {
            Debug.Assert(script.IsValid);

            // set additional $_SERVER items
            AddServerScriptItems(script);

            // remember the initial script file
            this.MainScriptFile = script;

            //

            try
            {
                if (Debugger.IsAttached)
                {
                    script.Evaluate(this, this.Globals, null);
                }
                else
                {
                    using (_requestTimer = new Timer(RequestTimeout, null, this.Configuration.Core.ExecutionTimeout, Timeout.Infinite))
                    {
                        script.Evaluate(this, this.Globals, null);
                    }
                }
            }
            catch (ScriptDiedException died)
            {
                died.ProcessStatus(this);
            }
        }

        void RequestTimeout(object state)
        {

        }

        void AddServerScriptItems(ScriptInfo script)
        {
            var array = this.Server;

            var path = script.Path.Replace('\\', '/');  // address of the script

            array["SCRIPT_FILENAME"] = (PhpValue)string.Concat(this.RootPath, "/", path);
            array["PHP_SELF"] = (PhpValue)string.Concat("/", path);
        }

        /// <summary>
        /// Disposes request resources.
        /// </summary>
        public override void Dispose()
        {
            base.Dispose();
        }

        #endregion

        public override IHttpPhpContext HttpPhpContext => this;

        public override Encoding StringEncoding => _encoding;
        readonly Encoding _encoding;

        /// <summary>
        /// Gets server type interface name.
        /// </summary>
        public override string ServerApi => "isapi";

        /// <summary>
        /// Reference to current <see cref="HttpContext"/>.
        /// Cannot be <c>null</c>.
        /// </summary>
        public HttpContext HttpContext => _httpctx;
        readonly HttpContext _httpctx;

        /// <summary>
        /// Internal timer used to cancel execution upon timeout.
        /// </summary>
        Timer _requestTimer;

        public RequestContextCore(HttpContext httpcontext, Stream responseStream, string rootPath, Encoding encoding)
        {
            Debug.Assert(httpcontext != null);
            Debug.Assert(encoding != null);

            _httpctx = httpcontext;
            _encoding = encoding;

            this.RootPath = rootPath;

            this.InitOutput(responseStream, new ResponseTextWriter(responseStream, encoding));
            this.InitSuperglobals();

            // TODO: start session if AutoStart is On
        }

        static void AddVariables(PhpArray target, IEnumerable<KeyValuePair<string, StringValues>> values)
        {
            foreach (var pair in values)
            {
                var strs = pair.Value;
                for (int i = 0; i < strs.Count; i++)
                {
                    Superglobals.AddVariable(target, pair.Key, strs[i]);
                }
            }
        }

        /// <summary>
        /// Loads $_SERVER from <see cref="_httpctx"/>.
        /// </summary>
        protected override PhpArray InitServerVariable()
        {
            var array = new PhpArray(32);

            var request = _httpctx.Request;

            //// adds variables defined by ASP.NET and IIS:
            //var serverVariables = _httpctx.Features.Get<IServerVariablesFeature>()?.ServerVariables;
            //if (serverVariables != null)
            //{
            //    foreach (string name in serverVariables)
            //    {
            //        // gets all values associated with the name:
            //        string[] values = serverVariables.GetValues(name);

            //        if (values == null)
            //            continue;   // http://phalanger.codeplex.com/workitem/30132

            //        // adds all items:
            //        if (name != null)
            //        {
            //            foreach (string value in values)
            //                Superglobals.AddVariable(array, name, value, null);
            //        }
            //        else
            //        {
            //            // if name is null, only name of the variable is stated:
            //            // e.g. for GET variables, URL looks like this: ...&test&...
            //            // we add the name of the variable and an emtpy string to get what PHP gets:
            //            foreach (string value in values)
            //            {
            //                Superglobals.AddVariable(array, value, string.Empty, null);
            //            }
            //        }
            //    }
            //}

            //// adds argv, argc variables:
            //if (RegisterArgcArgv)
            //{
            //    array["argv"] = PhpValue.Create(new PhpArray(1) { request.QueryString });
            //    array["argc"] = PhpValue.Create(0);
            //}

            // variables defined in PHP manual
            // order as it is by builtin PHP server
            array["DOCUMENT_ROOT"] = (PhpValue)RootPath;    // string, backslashes, no trailing slash

            //var f_connection = _httpctx.Features.Get<IHttpConnectionFeature>();
            array["REMOTE_ADDR"] = (PhpValue)(_httpctx.Connection.RemoteIpAddress?.ToString() ?? request.Headers["X-Real-IP"]);
            array["REMOTE_PORT"] = (PhpValue)_httpctx.Connection.RemotePort;
            array["LOCAL_ADDR"] = array["SERVER_ADDR"] = (PhpValue)_httpctx.Connection.LocalIpAddress?.ToString();
            array["LOCAL_PORT"] = (PhpValue)_httpctx.Connection.LocalPort;
            array["SERVER_SOFTWARE"] = (PhpValue)"ASP.NET Core Server";
            array["SERVER_PROTOCOL"] = (PhpValue)request.Protocol;
            array["SERVER_NAME"] = (PhpValue)request.Host.Host;
            array["SERVER_PORT"] = (PhpValue)(request.Host.Port ?? _httpctx.Connection.LocalPort);
            array["REQUEST_URI"] = (PhpValue)(request.Path.Value + request.QueryString.Value);
            array["REQUEST_METHOD"] = (PhpValue)request.Method;
            array["SCRIPT_NAME"] = (PhpValue)request.Path.ToString();
            array["SCRIPT_FILENAME"] = PhpValue.Null; // set in ProcessScript
            array["PHP_SELF"] = PhpValue.Null; // set in ProcessScript
            array["QUERY_STRING"] = (PhpValue)(request.QueryString.HasValue ? request.QueryString.Value.Substring(1) : string.Empty);
            array["HTTP_HOST"] = (PhpValue)request.Headers["Host"].ToString();
            array["HTTP_CONNECTION"] = (PhpValue)request.Headers["Connection"].ToString();
            array["HTTP_USER_AGENT"] = (PhpValue)request.Headers["User-Agent"].ToString();
            array["HTTP_ACCEPT"] = (PhpValue)request.Headers["Accept"].ToString();
            array["HTTP_ACCEPT_ENCODING"] = (PhpValue)request.Headers["Accept-Encoding"].ToString();
            array["HTTP_ACCEPT_LANGUAGE"] = (PhpValue)request.Headers["Accept-Language"].ToString();
            array["HTTP_REFERER"] = (PhpValue)request.Headers["Referer"].ToString();
            array["REQUEST_URI"] = (PhpValue)_httpctx.Features.Get<IHttpRequestFeature>()?.RawTarget;
            array["REQUEST_TIME_FLOAT"] = (PhpValue)DateTimeUtils.UtcToUnixTimeStampFloat(DateTime.UtcNow);
            array["REQUEST_TIME"] = (PhpValue)DateTimeUtils.UtcToUnixTimeStamp(DateTime.UtcNow);
            array["HTTPS"] = PhpValue.Create(request.IsHttps);

            //
            return array;
        }

        protected override PhpArray InitGetVariable()
        {
            var result = PhpArray.NewEmpty();

            if (_httpctx.Request.Method == "GET" && _httpctx.Request.HasFormContentType)
            {
                AddVariables(result, _httpctx.Request.Form);
            }

            AddVariables(result, _httpctx.Request.Query);

            //
            return result;
        }

        protected override PhpArray InitPostVariable()
        {
            var result = PhpArray.NewEmpty();

            if (_httpctx.Request.Method == "POST" && _httpctx.Request.HasFormContentType)
            {
                AddVariables(result, _httpctx.Request.Form);
            }

            return result;
        }

        protected override PhpArray InitFilesVariable()
        {
            PhpArray files;
            int count;

            if (_httpctx.Request.HasFormContentType && (count = _httpctx.Request.Form.Files.Count) != 0)
            {
                files = new PhpArray(count);

                // gets a path where temporary files are stored:
                var temppath = Path.GetTempPath(); // global_config.PostedFiles.GetTempPath(global_config.SafeMode);
                // temporary file name (first part)
                var basetempfilename = string.Concat("php_", DateTime.UtcNow.Ticks.ToString("x"), "-");
                var basetempfileid = this.GetHashCode();

                foreach (var file in _httpctx.Request.Form.Files)
                {
                    string file_path, type, file_name;
                    int error = 0;

                    if (!string.IsNullOrEmpty(file.FileName))
                    {
                        type = file.ContentType;

                        // CONSIDER: keep files in memory, use something like virtual fs (ASP.NET Core has one) or define post:// paths ?

                        var tempfilename = string.Concat(basetempfilename, (basetempfileid++).ToString("X"), ".tmp");
                        file_path = Path.Combine(temppath, tempfilename);
                        file_name = Path.GetFileName(file.FileName);

                        // registers the temporary file for deletion at request end:
                        AddTemporaryFile(file_path);

                        // saves uploaded content to the temporary file:
                        using (var stream = new FileStream(file_path, FileMode.Create))
                        {
                            file.CopyTo(stream);
                        }
                    }
                    else
                    {
                        file_path = type = file_name = string.Empty;
                        error = 4; // PostedFileError.NoFile;
                    }

                    //
                    files[file.Name] = (PhpValue)new PhpArray(5)
                    {
                        { "name", file_name },
                        { "type",type },
                        { "tmp_name",file_path },
                        { "error", error },
                        { "size", file.Length },
                    };
                }
            }
            else
            {
                files = PhpArray.NewEmpty();
            }

            //
            return files;
        }

        protected override PhpArray InitCookieVariable()
        {
            var result = PhpArray.NewEmpty();

            var cookies = _httpctx.Request.Cookies;
            if (cookies.Count != 0)
            {
                foreach (var c in cookies)
                {
                    Superglobals.AddVariable(result, c.Key, System.Net.WebUtility.UrlDecode(c.Value));
                }
            }

            //
            return result;
        }
    }
}
