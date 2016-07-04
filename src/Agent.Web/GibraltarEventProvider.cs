#region File Header

// <copyright file="GibraltarEventProvider.cs" company="Gibraltar Software Inc.">
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
using System.Collections;
using System.Diagnostics;
using System.Security.Principal;
using System.Text;
using System.Web;
using System.Web.Management;
using System.Xml;
using Gibraltar.Agent.Logging;
using Gibraltar.Agent.Web.Internal;

#endregion

namespace Gibraltar.Agent.Web
{
    /// <summary>
    /// A standard ASP.NET Health Event Provider that uses the Loupe infrastructure.
    /// </summary>
    public class GibraltarEventProvider 
        : WebEventProvider
    {
        internal const string LogSystem = "Gibraltar";
        private readonly bool m_NotInAspNet;

        /// <summary>
        /// Create a new instance of the health provider.
        /// </summary>
        public GibraltarEventProvider()
        {
            //see if we're being loaded from the ASPNET compiler and should NOT activate.
            try
            {
                string commandLine = Environment.CommandLine.ToLowerInvariant();

                if ((commandLine.Contains("aspnet_compiler.exe")) || (commandLine.Contains("aspnet_merge.exe")) || (commandLine.Contains("devenv.exe")))
                {
                    m_NotInAspNet = true;
                    Log.EndSession("The current process appears to be the ASP.NET Compiler or merge utilities, so we will disable the agent.");
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine(string.Format("Unable to determine command line information: ({0}): {1}", ex.GetType().FullName, ex.Message));
            }
        }

        #region Public Properties and Methods

        /// <summary>
        /// A text description of this event provider for administration tools
        /// </summary>
        public override string Description
        {
            get
            {
                return "ASP.NET Health Event Provider that uses the Loupe Application Monitoring infrastructure";
            }
        }

        /// <summary>
        /// Moves the events from the provider's buffer into Loupe. 
        /// </summary>
        public override void Flush()
        {
            //make SURE we're really running in the web server so we don't mess up aspnet_compiler.
            if (m_NotInAspNet)
                return;

            //right now we don't do anything.
        }

        /// <summary>
        /// Processes the event passed to the provider.
        /// </summary>
        /// <param name="raisedEvent"></param>
        public override void ProcessEvent(WebBaseEvent raisedEvent)
        {
            //make SURE we're really running in the web server so we don't mess up aspnet_compiler.
            if (m_NotInAspNet)
                return;

            try
            {
                //lets see if this is an event we're going to ignore to keep the log clean.
                if (raisedEvent.EventCode == WebEventCodes.AuditUrlAuthorizationSuccess)
                    return; //this is an ignorable audit success, who cares.
                if (raisedEvent.EventCode == WebEventCodes.AuditFileAuthorizationSuccess)
                    return; //this is an ignorable audit success, who cares.
                if (raisedEvent.EventCode == WebEventCodes.ApplicationHeartbeat)
                    return; //the user has other ways of knowing we're alive, we don't want to record this.
                if (raisedEvent.EventCode == WebEventCodes.ApplicationShutdown)
                {
                    //call end session and tell it our success information
                    Log.EndSession(SessionStatus.Normal, raisedEvent.Message);
                    return;
                }

                string categoryName = "System.Events." + GetEventCodeCategoryName(raisedEvent.EventCode);

                string caption = raisedEvent.Message;
                string description = null;
                
                Exception exception = null;
                LogMessageSeverity severity;

                Uri requestUri = null;
                WebProcessInformation processInformation = null;
                WebRequestInformation requestInformation = null;
                WebThreadInformation threadInformation = null;

                //process types we explicitly understand first

                WebSuccessAuditEvent webSuccessAuditEvent;
                WebViewStateFailureAuditEvent webViewStateFailureAuditEvent;
                WebFailureAuditEvent webFailureAuditEvent;
                WebRequestEvent webRequestEvent;
                WebRequestErrorEvent webRequestErrorEvent;
                WebErrorEvent webErrorEvent;
                WebBaseErrorEvent webBaseErrorEvent;
                WebApplicationLifetimeEvent webApplicationLifetimeEvent;
                WebManagementEvent webManagementEvent;

                if ((webSuccessAuditEvent = raisedEvent as WebSuccessAuditEvent) != null)
                {
                    //it's an otherwise unknown inheritor of the success event, treat it as such.

                    severity = LogMessageSeverity.Verbose;
                    processInformation = webSuccessAuditEvent.ProcessInformation;
                    requestInformation = webSuccessAuditEvent.RequestInformation;
                }
                else if ((webViewStateFailureAuditEvent = raisedEvent as WebViewStateFailureAuditEvent) != null)
                {
                    //it's an otherwise unknown inheritor of the failure audit event, treat it as such.

                    severity = LogMessageSeverity.Error;
                    exception = webViewStateFailureAuditEvent.ViewStateException;
                    GenerateLogMessage(exception, ref caption, ref description); //override the generic caption we created earlier.

                    processInformation = webViewStateFailureAuditEvent.ProcessInformation;
                    requestInformation = webViewStateFailureAuditEvent.RequestInformation;
                }
                else if ((webFailureAuditEvent = raisedEvent as WebFailureAuditEvent) != null)
                {
                    //it's an otherwise unknown inheritor of the failure event, treat it as such.

                    severity = LogMessageSeverity.Warning;

                    processInformation = webFailureAuditEvent.ProcessInformation;
                    requestInformation = webFailureAuditEvent.RequestInformation;
                }
                else if ((webRequestEvent = raisedEvent as WebRequestEvent) != null)
                {
                    //it's an otherwise unknown inheritor of the request event, treat it as such.

                    severity = LogMessageSeverity.Information;

                    processInformation = webRequestEvent.ProcessInformation;
                    requestInformation = webRequestEvent.RequestInformation;
                }
                else if ((webRequestErrorEvent = raisedEvent as WebRequestErrorEvent) != null)
                {
                    //it's an otherwise unknown inheritor of the request error event, treat it as such.

                    severity = LogMessageSeverity.Error;
                    exception = webRequestErrorEvent.ErrorException;
                    GenerateLogMessage(exception, ref caption, ref description); //override the generic caption we created earlier.

                    processInformation = webRequestErrorEvent.ProcessInformation;
                    requestInformation = webRequestErrorEvent.RequestInformation;
                    threadInformation = webRequestErrorEvent.ThreadInformation;
                }
                else if ((webErrorEvent = raisedEvent as WebErrorEvent) != null)
                {
                    //it's an otherwise unknown inheritor of the error event, treat it as such.

                    severity = LogMessageSeverity.Error;
                    exception = webErrorEvent.ErrorException;
                    GenerateLogMessage(exception, ref caption, ref description); //override the generic caption we created earlier.

                    processInformation = webErrorEvent.ProcessInformation;
                    requestInformation = webErrorEvent.RequestInformation;
                    threadInformation = webErrorEvent.ThreadInformation;
                }
                else if ((webBaseErrorEvent = raisedEvent as WebBaseErrorEvent) != null)
                {
                    //it's an otherwise unknown inheritor of the base error event, treat it as such.

                    exception = webBaseErrorEvent.ErrorException;
                    severity = LogMessageSeverity.Error;

                    processInformation = webBaseErrorEvent.ProcessInformation;
                }
                else if ((webApplicationLifetimeEvent = raisedEvent as WebApplicationLifetimeEvent) != null)
                {

                    //we use different severities for two scenarios:  Compilation and startup/shutdown
                    if ((raisedEvent.EventCode == WebEventCodes.ApplicationCompilationStart)
                        || (raisedEvent.EventCode == WebEventCodes.ApplicationCompilationEnd))
                    {
                        severity = LogMessageSeverity.Verbose;
                    }
                    else
                    {
                        severity = LogMessageSeverity.Information;
                    }

                    processInformation = webApplicationLifetimeEvent.ProcessInformation;
                }
                else if ((webManagementEvent = raisedEvent as WebManagementEvent) != null)
                {
                    //it's an otherwise unknown inheritor of the management event, treat it as such.

                    severity = LogMessageSeverity.Information;

                    processInformation = webManagementEvent.ProcessInformation;
                }
                else
                {
                    //overly simple initial implementation
                    severity = LogMessageSeverity.Information;
                }

                //see if we can populate more fields based on ASP-NET objects
                string userName = null;
                if (processInformation != null)
                {
                    userName = processInformation.AccountName;
                }

                if (threadInformation != null)
                {
                    //thread user name better than process user name
                    userName = threadInformation.ThreadAccountName;
                }

                string detailsXml = null;
                if (requestInformation != null)
                {
                    //if there is a current principal, that's our favorite answer.
                    IPrincipal currentPrincipal = requestInformation.Principal;
                    IIdentity currentIdentity = null;
                    if (currentPrincipal != null)
                    {
                        currentIdentity = currentPrincipal.Identity;
                    }

                    userName = currentIdentity != null ? currentIdentity.Name : requestInformation.ThreadAccountName;

                    detailsXml = GetRequestXml(requestInformation);

                    if (string.IsNullOrEmpty(requestInformation.RequestUrl) == false)
                    {
                        try
                        {
                            requestUri = new Uri(requestInformation.RequestUrl);
                        }
                        catch (Exception ex)
                        {
                            GC.KeepAlive(ex);
#if DEBUG
                            Log.RecordException(ex, "System", true);
#endif
                        }
                    }
                }

                //three ways we create our message source provider: Exception (best), Thread info (gotta parse, but accurate), and Request INfo (fake location)
                IMessageSourceProvider requestSource = null;
                if (exception != null)
                {
                    requestSource = new ExceptionSourceProvider(exception);
                }
                else if (requestInformation != null)
                {
                    requestSource = new WebRequestSourceProvider(requestInformation, requestUri);
                }
                else if (raisedEvent.EventSource != null)
                {
                    requestSource = new WebRequestSourceProvider(raisedEvent.EventSource);
                }

                //if we haven't figured out a class name by now, BS it using our event source.
                if ((requestSource as WebRequestSourceProvider != null) 
                    && (string.IsNullOrEmpty(requestSource.ClassName) && (raisedEvent.EventSource != null)))
                {
                    (requestSource as WebRequestSourceProvider).SetSource(raisedEvent.EventSource);
                }

                Log.Write(severity, LogSystem, requestSource, userName, exception, LogWriteMode.Queued, detailsXml, categoryName, caption, description);
            }
            catch (Exception ex)
            {
                GC.KeepAlive(ex);

#if DEBUG
                Log.RecordException(ex, "System", true);
#endif
            }
        }

        /// <summary>
        /// Performs tasks associated with shutting down the provider.
        /// </summary>
        public override void Shutdown()
        {
            Log.EndSession();
        }

        #endregion

        #region Private Properties and Methods

        /// <summary>
        /// Create an alternate message for an exception.
        /// </summary>
        /// <param name="ex">The original exception we want to base our message around</param>
        /// <param name="caption">The final caption for display</param>
        /// <param name="description">The final description for display</param>
        private static void GenerateLogMessage(Exception ex, ref string caption, ref string description)
        {
            if (ex == null)
                return; //everything stays the way it is.

            if (string.IsNullOrEmpty(ex.Message) == false)
            {
                //there is no message, go back to the original caption.
                caption = ex.Message;
            }

            if ((ex.Data != null) && (ex.Data.Count > 0))
            {
                StringBuilder descriptionBuilder = new StringBuilder(1024);
                descriptionBuilder.AppendLine("Details:");
                foreach (DictionaryEntry data in ex.Data)
                {
                    descriptionBuilder.AppendFormat("  {0}: {1}\r\n", data.Key ?? "(null)", data.Value ?? "(null)");
                }
                description = descriptionBuilder.ToString();
            }
            else
            {
                description = null;
            }
        }

        private static string GetEventCodeCategoryName(int eventCode)
        {
            string name;
            switch(eventCode)
            {
                case WebEventCodes.AuditFileAuthorizationFailure:
                case WebEventCodes.AuditFileAuthorizationSuccess:
                    name = "Audit.File";
                    break;
                case WebEventCodes.AuditFormsAuthenticationFailure:
                case WebEventCodes.AuditFormsAuthenticationSuccess:
                    name = "Audit.Forms";
                    break;
                case WebEventCodes.AuditMembershipAuthenticationFailure:
                case WebEventCodes.AuditMembershipAuthenticationSuccess:
                    name = "Audit.Membership";
                    break;
                case WebEventCodes.AuditInvalidViewStateFailure:
                case WebEventCodes.AuditUnhandledAccessException:
                case WebEventCodes.AuditUnhandledSecurityException:
                    name = "Audit.General";
                    break;
                case WebEventCodes.AuditUrlAuthorizationFailure:
                case WebEventCodes.AuditUrlAuthorizationSuccess:
                    name = "Audit.URL";
                    break;
                case WebEventCodes.ApplicationCompilationEnd:
                case WebEventCodes.ApplicationCompilationStart:
                case WebEventCodes.WebErrorCompilationError:
                    name = "Compilation";
                    break;
                case WebEventCodes.ApplicationStart:
                case WebEventCodes.ApplicationShutdown:
                case WebEventCodes.ApplicationHeartbeat:
                case WebEventCodes.WebErrorConfigurationError:
                    name = "Lifecycle";
                    break;
                case WebEventCodes.RequestTransactionAbort:
                case WebEventCodes.RequestTransactionComplete:
                    name = "Transaction";
                    break;
                case WebEventCodes.RuntimeErrorPostTooLarge:
                case WebEventCodes.RuntimeErrorRequestAbort:
                case WebEventCodes.RuntimeErrorUnhandledException:
                case WebEventCodes.RuntimeErrorValidationFailure:
                case WebEventCodes.RuntimeErrorViewStateFailure:
                case WebEventCodes.WebErrorObjectStateFormatterDeserializationError:
                case WebEventCodes.WebErrorParserError:
                case WebEventCodes.WebErrorPropertyDeserializationError:
                    name = "Page";
                    break;
                default:
                    name = "Application";
                    break;
            }

            return name;
        }

        private static string GetRequestXml(WebRequestInformation requestInformation)
        {
            //make sure we actually have something.
            if (requestInformation == null)
                return null;

            XmlDocument requestXml = new XmlDocument();
            XmlElement requestNode = requestXml.CreateElement("requestInformation");
            requestXml.AppendChild(requestNode);

            var sessionId = HttpContext.Current.Items["LoupeSessionId"] as string;
            var agentSessionId = HttpContext.Current.Items["LoupeAgentSessionId"] as string;

            if (!string.IsNullOrEmpty(sessionId))
            {
                XmlElement sessionIdNode = requestXml.CreateElement("sessionId");
                sessionIdNode.InnerText = sessionId;
                requestNode.AppendChild(sessionIdNode);
            }

            if (!string.IsNullOrEmpty(agentSessionId))
            {
                XmlElement agentSessionIdNode = requestXml.CreateElement("agentSessionId");
                agentSessionIdNode.InnerText = agentSessionId;
                requestNode.AppendChild(agentSessionIdNode);                
            }

            if (!string.IsNullOrEmpty(sessionId) && HttpContext.Current.Cache[sessionId] != null)
            {
                var clientDetails = HttpContext.Current.Cache[sessionId] as string;
                clientDetails = clientDetails.Substring(15, clientDetails.Length - 31);

                XmlElement clientDetailsNode = requestXml.CreateElement("clientDetails");
                clientDetailsNode.InnerXml = clientDetails;
                requestNode.AppendChild(clientDetailsNode);
            }

            if (string.IsNullOrEmpty(requestInformation.RequestUrl) == false)
            {
                XmlElement requestUrlNode = requestXml.CreateElement("requestUrl");
                requestUrlNode.InnerText = requestInformation.RequestUrl;
                requestNode.AppendChild(requestUrlNode);
            }

            if (string.IsNullOrEmpty(requestInformation.RequestPath) == false)
            {
                XmlElement requestPathNode = requestXml.CreateElement("requestPath");
                requestPathNode.InnerText = requestInformation.RequestPath;
                requestNode.AppendChild(requestPathNode);
            }

            if (string.IsNullOrEmpty(requestInformation.UserHostAddress) == false)
            {
                XmlElement userHostAddressNode = requestXml.CreateElement("userHostAddress");
                userHostAddressNode.InnerText = requestInformation.UserHostAddress;
                requestNode.AppendChild(userHostAddressNode);
            }

            if (string.IsNullOrEmpty(requestInformation.ThreadAccountName) == false)
            {
                XmlElement threadAccountNameNode = requestXml.CreateElement("threadAccountName");
                threadAccountNameNode.InnerText = requestInformation.ThreadAccountName;
                requestNode.AppendChild(threadAccountNameNode);
            }

            return requestXml.InnerXml;
        }

        #endregion
    }
}
