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

#if NET_4_5
using System.Threading.Tasks;
#endif

using System.Web;
using Gibraltar.Agent.Web.Internal;

#endregion

namespace Gibraltar.Agent.Web
{
    /// <summary>
    /// An ASP.NET HttpModule for performance tracking with Loupe.
    /// </summary>
    public class RequestTrackingModule : IHttpModule
    {
        private static readonly char[] Delimiters = new [] { '/', '\\' };
        private static readonly string[] ExcludedExtensions = new [] { "svg", "jpg", "jpeg", "gif", "png", "ico", "css", "js", "bmp"};

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
#if NET_4_5
                var taskAsyncHelper = new EventHandlerTaskAsyncHelper(BeginRequestAsync);
                context.AddOnBeginRequestAsync(taskAsyncHelper.BeginEventHandler, taskAsyncHelper.EndEventHandler);

                taskAsyncHelper = new EventHandlerTaskAsyncHelper(AuthenticateRequestAsync);
                context.AddOnAuthenticateRequestAsync(taskAsyncHelper.BeginEventHandler, taskAsyncHelper.EndEventHandler);

                taskAsyncHelper = new EventHandlerTaskAsyncHelper(PostAuthenticateRequestAsync);
                context.AddOnPostAuthenticateRequestAsync(taskAsyncHelper.BeginEventHandler, taskAsyncHelper.EndEventHandler);

                taskAsyncHelper = new EventHandlerTaskAsyncHelper(AuthorizeRequestAsync);
                context.AddOnAuthorizeRequestAsync(taskAsyncHelper.BeginEventHandler, taskAsyncHelper.EndEventHandler);

                taskAsyncHelper = new EventHandlerTaskAsyncHelper(PostAuthorizeRequestAsync);
                context.AddOnPostAuthorizeRequestAsync(taskAsyncHelper.BeginEventHandler, taskAsyncHelper.EndEventHandler);

                taskAsyncHelper = new EventHandlerTaskAsyncHelper(ResolveRequestCacheAsync);
                context.AddOnResolveRequestCacheAsync(taskAsyncHelper.BeginEventHandler, taskAsyncHelper.EndEventHandler);

                taskAsyncHelper = new EventHandlerTaskAsyncHelper(PostResolveRequestCacheAsync);
                context.AddOnPostResolveRequestCacheAsync(taskAsyncHelper.BeginEventHandler, taskAsyncHelper.EndEventHandler);

                taskAsyncHelper = new EventHandlerTaskAsyncHelper(AcquireRequestStateAsync);
                context.AddOnAcquireRequestStateAsync(taskAsyncHelper.BeginEventHandler, taskAsyncHelper.EndEventHandler);

                taskAsyncHelper = new EventHandlerTaskAsyncHelper(PostAcquireRequestStateAsync);
                context.AddOnPostAcquireRequestStateAsync(taskAsyncHelper.BeginEventHandler, taskAsyncHelper.EndEventHandler);

                taskAsyncHelper = new EventHandlerTaskAsyncHelper(PreRequestHandlerExecuteAsync);
                context.AddOnPreRequestHandlerExecuteAsync(taskAsyncHelper.BeginEventHandler, taskAsyncHelper.EndEventHandler);

                taskAsyncHelper = new EventHandlerTaskAsyncHelper(PostRequestHandlerExecuteAsync);
                context.AddOnPostRequestHandlerExecuteAsync(taskAsyncHelper.BeginEventHandler, taskAsyncHelper.EndEventHandler);

                taskAsyncHelper = new EventHandlerTaskAsyncHelper(ReleaseRequestStateAsync);
                context.AddOnReleaseRequestStateAsync(taskAsyncHelper.BeginEventHandler, taskAsyncHelper.EndEventHandler);

                taskAsyncHelper = new EventHandlerTaskAsyncHelper(PostReleaseRequestStateAsync);
                context.AddOnPostReleaseRequestStateAsync(taskAsyncHelper.BeginEventHandler, taskAsyncHelper.EndEventHandler);

                taskAsyncHelper = new EventHandlerTaskAsyncHelper(UpdateRequestCacheAsync);
                context.AddOnUpdateRequestCacheAsync(taskAsyncHelper.BeginEventHandler, taskAsyncHelper.EndEventHandler);

                taskAsyncHelper = new EventHandlerTaskAsyncHelper(PostUpdateRequestCacheAsync);
                context.AddOnPostUpdateRequestCacheAsync(taskAsyncHelper.BeginEventHandler, taskAsyncHelper.EndEventHandler);

                taskAsyncHelper = new EventHandlerTaskAsyncHelper(LogRequestAsync);
                context.AddOnLogRequestAsync(taskAsyncHelper.BeginEventHandler, taskAsyncHelper.EndEventHandler);

                taskAsyncHelper = new EventHandlerTaskAsyncHelper(PostLogRequestAsync);
                context.AddOnPostAcquireRequestStateAsync(taskAsyncHelper.BeginEventHandler, taskAsyncHelper.EndEventHandler);

                taskAsyncHelper = new EventHandlerTaskAsyncHelper(EndRequestAsync);
                context.AddOnEndRequestAsync(taskAsyncHelper.BeginEventHandler, taskAsyncHelper.EndEventHandler);
#endif

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
#if NET_4_5
                    context.MapRequestHandler += HttpApplicationMapRequest;
                    context.PostMapRequestHandler += HttpApplicationPostMapRequest;

                    taskAsyncHelper = new EventHandlerTaskAsyncHelper(MapRequestAsync);
                    context.AddOnMapRequestHandlerAsync(taskAsyncHelper.BeginEventHandler, taskAsyncHelper.EndEventHandler);

                    taskAsyncHelper = new EventHandlerTaskAsyncHelper(PostMapRequestAsync);
                    context.AddOnPostMapRequestHandlerAsync(taskAsyncHelper.BeginEventHandler, taskAsyncHelper.EndEventHandler);
#endif
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

        private static bool IsExcludedExtension(string extension)
        {
            //quick bail for obvious false case
            if (extension.Equals("aspx", StringComparison.Ordinal) || extension.Equals("asmx", StringComparison.Ordinal))
                return false;

            foreach (string excludedExtension in ExcludedExtensions)
            {
                if (extension.Equals(excludedExtension, StringComparison.Ordinal))
                    return true;
            }

            return false;
        }

        #endregion

#if NET_4_5

        private void HttpApplicationMapRequest(object sender, EventArgs e)
        {
            if (m_CurrentRequestMetric != null)
                m_CurrentRequestMetric.MapRequestStart();
        }

        private void HttpApplicationPostMapRequest(object sender, EventArgs e)
        {
            if (m_CurrentRequestMetric != null)
                m_CurrentRequestMetric.MapRequestEnd();
        }

#pragma warning disable 1998
        #region Async EventHandlers

        private async Task BeginRequestAsync(object sender, EventArgs e)
        {
            HttpApplicationBeginRequest(sender, e);
        }

        private async Task LogRequestAsync(object sender, EventArgs e)
        {
            HttpApplicationLogRequest(sender, e);
        }

        private async Task PostLogRequestAsync(object sender, EventArgs e)
        {
            HttpApplicationPostLogRequest(sender, e);
        }

        private async Task UpdateRequestCacheAsync(object sender, EventArgs e)
        {
            HttpApplicationUpdateRequestCache(sender, e);
        }

        private async Task PostUpdateRequestCacheAsync(object sender, EventArgs e)
        {
            HttpApplicationPostUpdateRequestCache(sender, e);
        }

        private async Task ReleaseRequestStateAsync(object sender, EventArgs e)
        {
            HttpApplicationReleaseRequestState(sender, e);
        }

        private async Task PostReleaseRequestStateAsync(object sender, EventArgs e)
        {
            HttpApplicationPostReleaseRequestState(sender, e);
        }

        private async Task PreRequestHandlerExecuteAsync(object sender, EventArgs e)
        {
            HttpApplicationPreRequestHandlerExecute(sender, e);
        }

        private async Task PostRequestHandlerExecuteAsync(object sender, EventArgs e)
        {
            HttpApplicationPostRequestHandlerExecute(sender, e);
        }

        private async Task AcquireRequestStateAsync(object sender, EventArgs e)
        {
            HttpApplicationAcquireRequestState(sender, e);
        }

        private async Task PostAcquireRequestStateAsync(object sender, EventArgs e)
        {
            HttpApplicationPostAcquireRequestState(sender, e);
        }

        private async Task ResolveRequestCacheAsync(object sender, EventArgs e)
        {
            HttpApplicationResolveRequestCache(sender, e);
        }

        private async Task PostResolveRequestCacheAsync(object sender, EventArgs e)
        {
            HttpApplicationPostResolveRequestCache(sender, e);
        }

        private async Task AuthorizeRequestAsync(object sender, EventArgs e)
        {
            HttpApplicationAuthorizeRequest(sender, e);
        }

        private async Task PostAuthorizeRequestAsync(object sender, EventArgs e)
        {
            HttpApplicationPostAuthorizeRequest(sender, e);
        }

        private async Task AuthenticateRequestAsync(object sender, EventArgs e)
        {
            HttpApplicationAuthenticateRequest(sender, e);
        }

        private async Task PostAuthenticateRequestAsync(object sender, EventArgs e)
        {
            HttpApplicationPostAuthenticateRequest(sender, e);
        }

        private async Task EndRequestAsync(object sender, EventArgs e)
        {
            HttpApplicationEndRequest(sender, e);
        }

        private async Task PostMapRequestAsync(object sender, EventArgs e)
        {
            HttpApplicationPostMapRequest(sender, e);
        }

        private async Task MapRequestAsync(object sender, EventArgs e)
        {
            HttpApplicationMapRequest(sender, e);
        }

        #endregion
#pragma warning restore 1998
#endif

        #region Event Handlers

        private void HttpApplicationAuthenticateRequest(object sender, EventArgs e)
        {
            if (m_CurrentRequestMetric != null)
                m_CurrentRequestMetric.AuthenticateStart();
        }

        private void HttpApplicationBeginRequest(object sender, EventArgs e)
        {
            //we could have a current request - if the last request aborted or ended early.
            if (m_CurrentRequestMetric != null)
            {
                //but we aren't going to consider its timing valid because who knows how long it sat in the queue.
                m_CurrentRequestMetric.Suppress();
                m_CurrentRequestMetric.Dispose();
            }

            //create a new metric.  We then go configure it, but we may set it back to null if this is an image or something.
            m_CurrentRequestMetric = new HttpRequestMetric();

            try
            {

                HttpApplication application = sender as HttpApplication;
                Debug.Assert(application != null);

                //figure out the page and the full path in our friendly form.
                HttpRequest request = application.Request;
                string fullAppRelativePath = request.AppRelativeCurrentExecutionFilePath;

                if (string.IsNullOrEmpty(fullAppRelativePath) == false)
                {

                    //but pull off the ~/ to get rid of silliness
                    if (fullAppRelativePath.StartsWith("~/"))
                    {
                        fullAppRelativePath = fullAppRelativePath.Substring(2);
                    }

                    // check if this is a call from a loupe JS agent
                    if (fullAppRelativePath == "loupe/log")
                    {
                        // we don't record requests from our own JS agents
                        // to avoid it cluttering the users metrics  so we 
                        // set the current request metric to null and exit
                        m_CurrentRequestMetric = null;
                        return;
                    }

                    m_CurrentRequestMetric.AbsolutePath = fullAppRelativePath;
                    m_CurrentRequestMetric.QueryString = request.Url.Query;

#if NET_4_5
                    if (string.IsNullOrWhiteSpace(fullAppRelativePath) == false)
#else
                    if (string.IsNullOrEmpty(fullAppRelativePath) == false)
#endif
                    {
                        //now see if we can find just the file name.
                        int fileStartIndex = fullAppRelativePath.LastIndexOfAny(Delimiters);
                        if (fileStartIndex == -1)
                            fileStartIndex = 0; //we never found a slash, so we start at zero.
                        else
                            fileStartIndex++; //because we don't really want the slash.

                        //and we don't want any extension for this pretty name...
                        int nameLength = fullAppRelativePath.Length - fileStartIndex;
                        int extensionIndex = fullAppRelativePath.LastIndexOf('.');
                        if (extensionIndex > fileStartIndex)
                        {
                            nameLength = (extensionIndex - fileStartIndex);

                            //and get the extension as well, so we can decide if we even care about this guy.
                            if (extensionIndex < (fullAppRelativePath.Length - 1))
                            {
                                string extension = fullAppRelativePath.Substring(extensionIndex + 1); //add the one to get rid of the period.
                                if ((string.IsNullOrEmpty(extension) == false) && (IsExcludedExtension(extension.ToLowerInvariant())))
                                    m_CurrentRequestMetric = null; //so we don't record jack.
                            }
                        }

                        if (m_CurrentRequestMetric != null)
                            m_CurrentRequestMetric.PageName = (nameLength > 0) ? fullAppRelativePath.Substring(fileStartIndex, nameLength) : string.Empty;
                    }

                    //careful!  at this point the current request may be null
                }
            }
            catch (Exception ex)
            {
                GC.KeepAlive(ex);

#if DEBUG
                Log.RecordException(ex, "System", true);
#endif
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
                if (HttpContext.Current != null)
                {
                    if (HttpContext.Current.User != null)
                    {
                        m_CurrentRequestMetric.UserName = HttpContext.Current.User.Identity.Name;
                    }

                    m_CurrentRequestMetric.SessionId = HttpContext.Current.Items["LoupeSessionId"] as string;
                    m_CurrentRequestMetric.AgentSessionId = HttpContext.Current.Items["LoupeAgentSessionId"] as string;
                }

                //dispose the metric to have it record itself and then clear our pointer.
                m_CurrentRequestMetric.Dispose();
                m_CurrentRequestMetric = null;
            }
        }

#endregion
    }
}
