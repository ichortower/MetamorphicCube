using ichortower.TowerCore;

using TR = ichortower.TowerCore.Translation;

namespace ichortower.MetamorphicCube;

internal class Tokens
{
    internal static string Parse(string input)
    {
        if (input is null) {
            return input;
        }
        string ret = input.Replace("{{ModId}}", Main.ModId);
        int loc = 0;
        while ((loc = ret.IndexOf("{{i18n:", loc)) != -1) {
            int end = ret.IndexOf("}}", loc);
            if (end == -1) {
                Log.Warn($"Error parsing '{input}': unterminated i18n token");
                break;
            }
            string key = ret[(loc + "{{i18n:".Length)..(end)];
            ret = ret.Remove(loc, end + "}}".Length - loc).Insert(loc, TR.Get(key));
        }
        return ret;
    }
}
