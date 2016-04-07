using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.Remoting;
using System.Text.RegularExpressions;
using Inversion.Collections;
using Inversion.Razor.Extensions;
using Inversion.Razor.Model;
using Inversion.Web;
using RazorEngine.Templating;
using FullPathTemplateKey = RazorEngine.Templating.FullPathTemplateKey;
using NameOnlyTemplateKey = RazorEngine.Templating.NameOnlyTemplateKey;

namespace Inversion.Razor.Plugins
{
    /// <summary>
    /// A RazorView plugin that parses a template for @Include commands.
    /// Matched commands will then be attempted to be compiled, ready for RazorEngine to use them.
    /// @Include statements are expected to be relative in the source and will be replaced inline with an absolute path to the included template.
    /// </summary>
    public class RazorViewIncludePlugin : IRazorViewPlugin
    {
        private static Regex _regexInclude = new Regex("\\@Include\\([\\@]?\"(.*?)\"", RegexOptions.Compiled);

        public string Execute(IWebContext context, IDictionary<string, string> parameters, string source)
        {
            TokenList tokenList = new TokenList();

            foreach (Match match in _regexInclude.Matches(source))
            {
                if (match.Success)
                {
                    string includeName = match.Groups[1].Value;

                    string safeIncludeName = includeName.FixPathSeparatorChars();

                    string templateFolder = parameters["templatefolder"];

                    string includePath = Path.Combine(templateFolder, safeIncludeName);

                    Type modelType = typeof(DataDictionary<IData>);

                    ITemplateKey tk = new NameOnlyTemplateKey(includePath, ResolveType.Include, null);

                    if(!RazorEngine.Engine.Razor.IsTemplateCached(tk, modelType))
                    { // we'll need to look for the template
                        if (File.Exists(includePath))
                        {
                            string include = File.ReadAllText(includePath);

                            if (_regexInclude.IsMatch(include))
                            {
                                include = this.Execute(context, parameters, include);
                            }

                            RazorEngine.Engine.Razor.Compile(include, tk, modelType);
                        }
                    }

                    tokenList.AddOrUpdate(includeName, includePath);
                }
            }

            return tokenList.Replace(source);
        }
    }
}