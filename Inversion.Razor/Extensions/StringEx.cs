namespace Inversion.Razor.Extensions
{
    public static class StringEx
    {
        public static string FixPathSeparatorChars(this string path)
        {
            return path.Replace('\\', System.IO.Path.DirectorySeparatorChar);
        }
    }
}
