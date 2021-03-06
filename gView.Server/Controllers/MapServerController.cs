﻿using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using gView.Framework.system;
using gView.MapServer;
using gView.Server.AppCode;
using Microsoft.AspNetCore.Mvc;

namespace gView.Server.Controllers
{
    public class MapServerController : Controller
    {
        public IActionResult Index()
        {
            return View();
        }

        public IActionResult Catalog(string format)
        {
            try
            {
                //string user, pwd;
                //var request = Request(out user, out pwd);

                if (format == "xml")
                {
                    StringBuilder sb = new StringBuilder();
                    sb.Append("<RESPONSE><SERVICES>");
                    foreach (var service in InternetMapServer.Instance.Maps)
                    {
                        sb.Append("<SERVICE ");
                        sb.Append("NAME='" + service.Name + "' ");
                        sb.Append("name='" + service.Name + "' ");
                        sb.Append("type='" + service.Type.ToString() + "' ");
                        sb.Append("/>");
                    }
                    sb.Append("</SERVICES></RESPONSE>");

                    return Result(sb.ToString(), "text/xml");
                }
                else
                {
                    throw new Exception("Unknown format");
                }
            }
            catch (UnauthorizedAccessException)
            {
                return WriteUnauthorized();
            }
            catch(Exception ex)
            {
                return WriteError(ex.Message);
            }
        }

        public IActionResult MapRequest(string guid, string id)
        {
            try
            {
                if (IfMatch())
                {
                    return NotModified();
                }

                //DateTime td = DateTime.Now;
                //Console.WriteLine("Start Map Request " + td.ToLongTimeString() + "." + td.Millisecond + " (" + name + ")");
                //System.Threading.Thread.Sleep(10000);

                //string user, pwd;
                //var request = Request(out user, out pwd);

                #region Request

                string input = String.Empty;
                if (Request.Body.CanRead)
                {
                    MemoryStream ms = new MemoryStream();

                    byte[] bodyData = new byte[1024];
                    int bytesRead;
                    while ((bytesRead = Request.Body.Read(bodyData, 0, bodyData.Length)) > 0)
                    {
                        ms.Write(bodyData, 0, bytesRead);
                    }
                    input = Encoding.UTF8.GetString(ms.ToArray());
                }
                if (String.IsNullOrEmpty(input))
                    input = this.Request.QueryString.ToString();
                if (input.StartsWith("?"))
                    input = input.Substring(1);

                ServiceRequest serviceRequest = new ServiceRequest(id, input);
                serviceRequest.OnlineResource = InternetMapServer.OnlineResource + "/MapRequest/" + guid + "/" + id;

                #endregion

                IServiceRequestInterpreter interpreter =
                    InternetMapServer.GetInterpreter(new Guid(guid));

                #region Security

                Identity identity = Identity.FromFormattedString(String.Empty);
                identity.HashedPassword = String.Empty;
                serviceRequest.Identity = identity;

                #endregion

                #region Queue & Wait

                IServiceRequestContext context = new ServiceRequestContext(
                    InternetMapServer.Instance,
                    interpreter,
                    serviceRequest);

                InternetMapServer.ThreadQueue.AddQueuedThreadSync(interpreter.Request, context);

                #endregion

                string ret = serviceRequest.Response;

                return Result(ret, "text/xml");
            }
            catch (UnauthorizedAccessException)
            {
                return WriteUnauthorized();
            }
        }

        #region Manage

        [HttpPost]
        public IActionResult AddMap(string name)
        {
            try
            {
                string input = GetBody();


                string user=String.Empty, pwd=String.Empty;
                //var request = Request(out user, out pwd);

                bool ret = InternetMapServer.AddMap(name, input, user, pwd);

                return Result(ret.ToString(), "text/plain");
            }
            catch (UnauthorizedAccessException)
            {
                return WriteUnauthorized();
            }
        }

        [HttpPost]
        public IActionResult RemoveMap(string name)
        {
            try
            {
                string user=String.Empty, pwd=String.Empty;
                // var request = Request(out user, out pwd);

                bool ret = InternetMapServer.RemoveMap(name, user, pwd);

                return Result(ret.ToString(), "text/plain");
            }
            catch (UnauthorizedAccessException)
            {
                return WriteUnauthorized();
            }
        }

        public IActionResult GetMetadata(string name)
        {
            try
            {
                string user = String.Empty, pwd = String.Empty;
                // var request = Request(out user, out pwd);

                string ret = InternetMapServer.GetMetadata(name, user, pwd);

                return Result(ret, "text/xml");
            }
            catch (UnauthorizedAccessException)
            {
                return WriteUnauthorized();
            }
        }

        [HttpPost]
        public IActionResult SetMetadata(string name)
        {
            try
            {
                string input = GetBody();

                string user = String.Empty, pwd = String.Empty;
                // var request = Request(out user, out pwd);

                bool ret = InternetMapServer.SetMetadata(name, input, user, pwd);

                return Result(ret.ToString(), "text/plain");
            }
            catch (UnauthorizedAccessException)
            {
                return WriteUnauthorized();
            }
        }

        #endregion

        #region Helper

        private IActionResult WriteUnauthorized()
        {
            return WriteError("Unauthorized");
        }

        private IActionResult WriteError(string message)
        {
            return Result("<ERROR>" + message + "</ERROR>", "text/xml");
        }

        private string GetBody()
        {
            if (Request.Body.CanRead)
            {
                MemoryStream ms = new MemoryStream();

                byte[] bodyData = new byte[1024];
                int bytesRead;
                while ((bytesRead = Request.Body.Read(bodyData, 0, bodyData.Length)) > 0)
                {
                    ms.Write(bodyData, 0, bytesRead);
                }
                return Encoding.UTF8.GetString(ms.ToArray());
            }

            return String.Empty;
        }

        private IActionResult Result(string response, string contentType)
        {
            byte[] data = null;
            if (response.StartsWith("base64:"))
            {
                response = response.Substring("base64:".Length);
                if (response.Contains(":"))
                {
                    int pos = response.IndexOf(":");
                    contentType = response.Substring(0, pos);
                    response = response.Substring(pos + 1);
                }
                data = Convert.FromBase64String(response);
            }
            else if(response.StartsWith("{"))
            {
                try
                {
                    var mapServerResponse = gView.Framework.system.MapServerResponse.FromString(response);

                    if (mapServerResponse.Expires != null)
                        AppendEtag((DateTime)mapServerResponse.Expires);

                    return Result(mapServerResponse.Data, mapServerResponse.ContentType);
                }
                catch { }
            }
            else
            {
                data = Encoding.UTF8.GetBytes(response);
            }
            return Result(data, contentType);
        }

        private IActionResult Result(byte[] data, string contentType)
        {
            ViewData["content-type"] = contentType;
            ViewData["data"] = data;

            return View("_binary");
        }

        #region ETag

        private bool HasIfNonMatch()
        {
            return this.Request.Headers["If-None-Match"].ToString() != null;
        }

        private bool IfMatch()
        {
            try
            {
                if (HasIfNonMatch() == false)
                    return false;

                var etag = long.Parse(this.Request.Headers["If-None-Match"].ToString());

                DateTime etagTime = new DateTime(etag, DateTimeKind.Utc);
                if (DateTime.UtcNow > etagTime)
                    return false;

                return true;
            }
            catch
            {
                return false;
            }
        }

        private void AppendEtag(DateTime expires)
        {
            this.Request.Headers.Add("ETag", expires.Ticks.ToString());
            this.Request.Headers.Add("Last-Modified", DateTime.UtcNow.ToString("R"));
            this.Request.Headers.Add("Expires", expires.ToString("R"));

            //this.Response.CacheControl = "private";
            //this.Response.Cache.SetMaxAge(new TimeSpan(24, 0, 0));
        }

        protected IActionResult NotModified()
        {
            Response.StatusCode = 304;
            return Content(String.Empty);
        }

        #endregion

        #endregion
    }
}