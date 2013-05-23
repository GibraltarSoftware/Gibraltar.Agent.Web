#region File Header

// <copyright file="RequestTrackingModule.cs" company="Gibraltar Software Inc.">
// Gibraltar Software Inc. All rights reserved.
// 
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
// 
//     http://www.apache.org/licenses/LICENSE-2.0
// 
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.
// </copyright>

using System;
using System.Diagnostics;
using System.Web;
using Gibraltar.Agent.Web.Internal;

#endregion

namespace Gibraltar.Agent.Web
{
    using System.Threading;

    /// <summary>
    /// An ASP.NET HttpModule for performance tracking with Gibraltar.
    /// </summary>
    public class RequestTrackingModule : IHttpModule
    {
        private static readonly char[] Delimiters = new [] { '/', '\\' };
        private static readonly HashSet<string> ExcludedExtensions = new HashSet<string>(StringComparer.OrdinalIgnoreCase) {"jpg", "jpeg", "gif", "png", "ico", "css", "js", "bmp"};

        private HttpRequestMetric m_CurrentRequestMetric;

        /// <summary>
        /// Disposes of the resources (other than memory) used by the module that implements <see cref="T:System.Web.IHttpModule"/>.
        /// </summary>
        public void Dispose()
        {
            try
            {
                if (m_CurrentRequestMetric != null)
                {
                    m_CurrentRequestMetric.Suppress();
                    m_CurrentRequestMetric.Dispose();
                    m_CurrentRequestMetric = null;
                }
            }
            catch
            {}
        }

        /// <summary>
        /// Initializes a module and prepares it to handle requests.
        /// </summary>
        /// <param name="context">An <see cref="T:System.Web.HttpApplication"/> that provides access to the methods, properties, and events common to all application objects within an ASP.NET application 
        ///                 </param>
        public void Init(HttpApplication context)
        {
            try
            {
                //we should not have a current request metric - we aren't supposed to get called until the last request completes.
                Debug.Assert(m_CurrentRequestMetric == null);
                context.BeginRequest += HttpApplicationBeginRequest;
                context.AuthenticateRequest += HttpApplicationAuthenticateRequest;
                context.PostAuthenticateRequest += HttpApplicationPostAuthenticateRequest;
                context.AuthorizeRequest += HttpApplicationAuthorizeRequest;
                context.PostAuthorizeRequest += HttpApplicationPostAuthorizeRequest;
                context.ResolveRequestCache += HttpApplicationResolveRequestCache;
                context.PostResolveRequestCache += HttpApplicationPostResolveRequestCache;
                context.AcquireRequestState += HttpApplicationAcquireRequestState;
                context.PostAcquireRequestState += HttpApplicationPostAcquireRequestState;
                context.PreRequestHandlerExecute += HttpApplicationPreRequestHandlerExecute;
                context.PostRequestHandlerExecute += HttpApplicationPostRequestHandlerExecute;
                context.ReleaseRequestState += HttpApplicationReleaseRequestState;
                context.PostReleaseRequestState += HttpApplicationPostReleaseRequestState;
                context.UpdateRequestCache += HttpApplicationUpdateRequestCache;
                context.PostUpdateRequestCache += HttpApplicationPostUpdateRequestCache;
                try
                {
                    context.LogRequest += HttpApplicationLogRequest;
                    context.PostLogRequest += HttpApplicationPostLogRequest;
                }
                catch (Exception ex)
                {
                    GC.KeepAlive(ex);
#if DEBUG
                    Log.Information(ex, "System", "Unable to time request logging", "We're probably running in a version of IIS older than 7 or not in Integrated pipeline mode, because an exception was thrown when we attempted to bind to the LogRequest events.  Exception:\r\n {0}", ex.Message);
#endif
                }
                context.EndRequest += HttpApplicationEndRequest;

            }
            catch (Exception ex)
            {
                GC.KeepAlive(ex);
#if DEBUG
                Log.RecordException(ex, "System", true);
#endif
            }
        }

        #region Private Properties and Methods

        internal static bool IsExcludedExtension(string extension)
        {
            return ExcludedExtensions.Contains(extension);
        }

        #endregion

        #region Event Handlers

        private void HttpApplicationAuthenticateRequest(object sender, EventArgs e)
        {
            if (m_CurrentRequestMetric != null)
                m_CurrentRequestMetric.AuthenticateStart();
        }

        private void HttpApplicationBeginRequest(object sender, EventArgs e)
        {
            //we could have a current request - if the last request aborted or ended early.
            var oldMetric = Interlocked.Exchange(ref m_CurrentRequestMetric, null);
            if (oldMetric != null)
            {
                //but we aren't going to consider its timing valid because who knows how long it sat in the queue.
                oldMetric.Suppress();
                oldMetric.Dispose();
            }

            try
            {
                HttpApplication application = sender as HttpApplication;
                Debug.Assert(application != null);

                //figure out the page and the full path in our friendly form.
                HttpRequest request = application.Request;
                string fullAppRelativePath = request.AppRelativeCurrentExecutionFilePath;
                string queryString = request.Url.Query;

                m_CurrentRequestMetric = BuildRequestMetric(fullAppRelativePath, queryString);
            }
            catch (Exception ex)
            {
                GC.KeepAlive(ex);

#if DEBUG
                Log.RecordException(ex, "System", true);
#endif
            }
        }

        internal static HttpRequestMetric BuildRequestMetric(string fullAppRelativePath, string queryString)
        {
            if (string.IsNullOrEmpty(fullAppRelativePath))
            {
                return new HttpRequestMetric();
            }
            {
                //but pull off the ~/ to get rid of silliness
                if (fullAppRelativePath.StartsWith("~/"))
                    fullAppRelativePath = fullAppRelativePath.Substring(2);


                //now see if we can find just the file name.
                int fileStartIndex = fullAppRelativePath.LastIndexOfAny(Delimiters);
                if (fileStartIndex == -1)
                    fileStartIndex = 0; //we never found a slash, so we start at zero.
                else
                    fileStartIndex++; //because we don't really want the slash.

                //and we don't want any extension for this pretty name...
                int extensionIndex = fullAppRelativePath.IndexOf('.', fileStartIndex);
                if (extensionIndex == -1)
                    extensionIndex = fullAppRelativePath.Length - 1; //so we go to the end.

                //and get the extension as well, so we can decide if we even care about this guy.
                if (extensionIndex < (fullAppRelativePath.Length - 1))
                {
                    string extension = fullAppRelativePath.Substring(extensionIndex + 1); //add the one to get rid of the period.

                    if ((string.IsNullOrEmpty(extension) == false) && (IsExcludedExtension(extension.ToLowerInvariant())))
                        return null;
                }

                int nameLength = (extensionIndex - fileStartIndex);

                return new HttpRequestMetric
                           {
                               AbsolutePath = fullAppRelativePath,
                               PageName = (nameLength > 0) ? fullAppRelativePath.Substring(fileStartIndex, (extensionIndex - fileStartIndex)) : string.Empty,
                               QueryString = queryString,
                           };
                //careful!  at this point the current request may be null
            }
        }

        private void HttpApplicationPostAuthenticateRequest(object sender, EventArgs e)
        {
            if (m_CurrentRequestMetric != null)
                m_CurrentRequestMetric.AuthenticateEnd();
        }

        private void HttpApplicationAuthorizeRequest(object sender, EventArgs e)
        {
            if (m_CurrentRequestMetric != null)
                m_CurrentRequestMetric.AuthorizeRequestStart();
        }

        private void HttpApplicationPostAuthorizeRequest(object sender, EventArgs e)
        {
            if (m_CurrentRequestMetric != null)
                m_CurrentRequestMetric.AuthorizeRequestEnd();
        }

        private void HttpApplicationResolveRequestCache(object sender, EventArgs e)
        {
            if (m_CurrentRequestMetric != null)
                m_CurrentRequestMetric.ResolveRequestCacheStart();
        }

        private void HttpApplicationPostResolveRequestCache(object sender, EventArgs e)
        {
            if (m_CurrentRequestMetric != null)
                m_CurrentRequestMetric.ResolveRequestCacheEnd();
        }

        private void HttpApplicationAcquireRequestState(object sender, EventArgs e)
        {
            if (m_CurrentRequestMetric != null)
                m_CurrentRequestMetric.AcquireRequestStateStart();
        }

        private void HttpApplicationPostAcquireRequestState(object sender, EventArgs e)
        {
            if (m_CurrentRequestMetric != null)
                m_CurrentRequestMetric.AcquireRequestStateEnd();
        }

        private void HttpApplicationPreRequestHandlerExecute(object sender, EventArgs e)
        {
            if (m_CurrentRequestMetric != null)
                m_CurrentRequestMetric.RequestHandlerExecuteStart();
        }

        private void HttpApplicationPostRequestHandlerExecute(object sender, EventArgs e)
        {
            if (m_CurrentRequestMetric != null)
                m_CurrentRequestMetric.RequestHandlerExecuteEnd();
        }

        private void HttpApplicationReleaseRequestState(object sender, EventArgs e)
        {
            if (m_CurrentRequestMetric != null)
                m_CurrentRequestMetric.ReleaseRequestStateStart();
        }

        private void HttpApplicationPostReleaseRequestState(object sender, EventArgs e)
        {
            if (m_CurrentRequestMetric != null)
                m_CurrentRequestMetric.ReleaseRequestStateEnd();
        }

        private void HttpApplicationUpdateRequestCache(object sender, EventArgs e)
        {
            if (m_CurrentRequestMetric != null)
                m_CurrentRequestMetric.UpdateRequestCacheStart();
        }

        private void HttpApplicationPostUpdateRequestCache(object sender, EventArgs e)
        {
            if (m_CurrentRequestMetric != null)
                m_CurrentRequestMetric.UpdateRequestCacheEnd();
        }

        private void HttpApplicationLogRequest(object sender, EventArgs e)
        {
            if (m_CurrentRequestMetric != null)
                m_CurrentRequestMetric.LogRequestStart();
        }

        private void HttpApplicationPostLogRequest(object sender, EventArgs e)
        {
            if (m_CurrentRequestMetric != null)
                m_CurrentRequestMetric.LogRequestEnd();
        }

        private void HttpApplicationEndRequest(object sender, EventArgs e)
        {
            if (m_CurrentRequestMetric != null)
            {
                //dispose the metric to have it record itself and then clear our pointer.
                m_CurrentRequestMetric.Dispose();
                m_CurrentRequestMetric = null;
            }
        }

        #endregion
    }
}
