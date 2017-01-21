using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;

using log4net;

using RazorEngine.Templating;

using Inversion.Collections;
using Inversion.Process;
using Inversion.Razor.Plugins;
using Inversion.Web;
using Inversion.Web.Behaviour;

namespace Inversion.Razor.Behaviour
{
    public class RazorViewBehaviour : WebBehaviour
    {
        private static readonly ILog _log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

        private readonly string _contentType;
        private readonly IList<IRazorViewPlugin> _templatePlugins;

        /// <summary>
        /// 
        /// </summary>
        /// <param name="message"></param>
        /// <param name="templatePlugins"></param>
        /// <remarks>
        /// This constructor defaults the content type to `text/html`.
        /// </remarks>
        public RazorViewBehaviour(string message, IList<IRazorViewPlugin> templatePlugins) : this(message, "text/html", templatePlugins) { }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="message"></param>
        /// <param name="contentType"></param>
        /// <param name="templatePlugins"></param>
        public RazorViewBehaviour(string message, string contentType, IList<IRazorViewPlugin> templatePlugins)
            : base(message)
        {
            _contentType = contentType;
            _templatePlugins = templatePlugins ?? new List<IRazorViewPlugin>();
        }

        // TODO: confirm thread safe
        // The iterator generated for this should be
        //		ThreadLocal and therefore safe to use
        //		in this manner on a singleton, would be
        //		nice to confirm this.
        private IEnumerable<string> _possibleTemplates(IWebContext context)
        {
            string area = context.Params["area"];
            string concern = context.Params["concern"];
            string action = String.Format("{0}.cshtml", context.Params["action"]);

            // area/concern/action
            yield return Path.Combine(area, concern, action);
            yield return Path.Combine(area, concern, "default.cshtml");
            // area/action
            yield return Path.Combine(area, action);
            yield return Path.Combine(area, "default.cshtml");
            // action
            yield return action;
            yield return "default.cshtml";

        }

        public override void Action(IEvent ev, IWebContext context)
        {

            if (context.ViewSteps.HasSteps && context.ViewSteps.Last.HasModel)
            { // we should have a model that we're going to render
                string content = String.Empty; // default output if we can't process a template

                foreach (string templateName in _possibleTemplates(context))
                { // check each possible template in turn
                    // This is the most immediate way I found to check if a template has been compiled
                    //		but we don't actually use the returned template as I can't find how to get a razor context
                    //		to run against, when I do this code will probably change with a possible early bail here.
                    
                    IDictionary<string, string> templateParameters = new Dictionary<string, string>();
                    templateParameters.Add("templatename", templateName);
                    templateParameters.Add("templatefolder", Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Resources", "Views", "Razor"));
                    templateParameters.Add("templatepath", Path.Combine(templateParameters["templatefolder"], templateParameters["templatename"]));

                    Type modelType = typeof(DataDictionary<IData>);

                    ITemplateKey tk = new NameOnlyTemplateKey(templateParameters["templatepath"], ResolveType.Global, null);

                    bool cached = RazorEngine.Engine.Razor.IsTemplateCached(tk, modelType);
                    
                    if(!cached)
                    {
                        string correctedFilename = String.Empty;
                        if (this.FileExistsCaseInsensitive(templateParameters["templatepath"], out correctedFilename))
                        {
                            string template = File.ReadAllText(correctedFilename);
                            try
                            {
                                template = ExecutePlugins(context, templateParameters, template);
                                RazorEngine.Engine.Razor.Compile(template, tk, modelType);
                            }
                            catch (Exception e)
                            {
                                _log.DebugFormat("execute or compile threw: {0}", e.ToString());
                                continue;
                            }
                        }
                        else
                        {
                            //_log.DebugFormat("template did not exist");
                            continue;
                        }
                    }

                    try
                    {
                        _log.DebugFormat("template run for {0}", templateParameters["templatepath"]);

                        content = RazorEngine.Engine.Razor.Run(tk, modelType, context.ViewSteps.Last.Model);
                    }
                    catch (Exception e)
                    {
                        _log.DebugFormat("template run threw {0}", e.ToString());
                        break;
                    }
                    
                    if (!String.IsNullOrEmpty(content))
                    { // we have content so create the view step and bail
                        context.ViewSteps.CreateStep(templateParameters["templatename"], _contentType, content);
                        break;
                    }
                }
            }
        }

        private string ExecutePlugins(IWebContext context, IDictionary<string, string> parameters, string source)
        {
            return _templatePlugins.Aggregate(source, (current, plugin) => plugin.Execute(context, parameters, current));
        }

        private bool FileExistsCaseInsensitive(string fileName, out string correctedFilename)
        {
            correctedFilename = fileName;

            string directory = Path.GetDirectoryName(fileName);
            string fileTitle = Path.GetFileName(fileName);
            
            if(Directory.Exists(directory))
            {
                string[] files = Directory.GetFiles(directory);
                
                if (files.GetLength(0) > 0)
                {
                    foreach(string f in files)
                    {
                        string ft = Path.GetFileName(f);

                        if (String.Equals(ft, fileTitle, StringComparison.InvariantCultureIgnoreCase))
                        {
                            correctedFilename = f;

                            return File.Exists(correctedFilename);
                        }
                    }
                }
            }

            return false;
        }
    }
}