using System.Collections.Generic;

using Inversion.Web;

namespace Inversion.Razor.Plugins
{
    public interface IRazorViewPlugin
    {
        string Execute(IWebContext context, IDictionary<string, string> parameters, string source);
    }
}