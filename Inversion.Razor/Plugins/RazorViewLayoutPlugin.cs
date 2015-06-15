using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Inversion.Collections;
using Inversion.Razor.Extensions;
using Inversion.Razor.Model;
using Inversion.Web;
using RazorEngine.Templating;
using NameOnlyTemplateKey = RazorEngine.Templating.NameOnlyTemplateKey;

namespace Inversion.Razor.Plugins
{
    /// <summary>
    /// A RazorView plugin that parses a template for the 'this.Layout = @"(path to layout file)"' command.
    /// A matched layout will then be attempted to be compiled, ready for RazorEngine to use it.
    /// The path to the layout file is expected to be relative to the base Resources\Views\Razor folder.
    /// </summary>
    public class RazorViewLayoutPlugin : IRazorViewPlugin
    {
        private static Regex _regexLayout = new Regex("this\\.Layout = \\@\"(.*?)\";", RegexOptions.Compiled);

        private readonly IList<IRazorViewPlugin> _layoutPlugins;

        public RazorViewLayoutPlugin()
        {
            _layoutPlugins = new List<IRazorViewPlugin>();
        }

        public RazorViewLayoutPlugin(IList<IRazorViewPlugin> layoutPlugins)
        {
            _layoutPlugins = layoutPlugins;
        }

        public string Execute(IWebContext context, IDictionary<string, string> parameters, string source)
        {
            Match match = _regexLayout.Match(source);

            if (match.Success)
            {
                string layoutName = match.Groups[1].Value;

                string safeLayoutName = layoutName.FixPathSeparatorChars();

                string templateFolder = parameters["templatefolder"];

                string layoutPath = Path.Combine(templateFolder, safeLayoutName);

                Type modelType = typeof(DataDictionary<IData>);

                ITemplateKey tk = new NameOnlyTemplateKey(layoutName, ResolveType.Layout, null);

                if (!RazorEngine.Engine.Razor.IsTemplateCached(tk, modelType))
                { // we'll need to look for the template
                    if (File.Exists(layoutPath))
                    {
                        string layout = File.ReadAllText(layoutPath);
                        IDictionary<string, string> layoutParameters = new Dictionary<string, string>(parameters);
                        layoutParameters["templatename"] = safeLayoutName;
                        layoutParameters["templatepath"] = layoutPath;
                        layout = ExecutePlugins(context, layoutParameters, layout);
                        RazorEngine.Engine.Razor.Compile(layout, tk, modelType);
                    }
                }
            }

            return source;
        }

        private string ExecutePlugins(IWebContext context, IDictionary<string, string> parameters, string source)
        {
            return _layoutPlugins.Aggregate(source, (current, plugin) => plugin.Execute(context, parameters, current));
        }

    }
}