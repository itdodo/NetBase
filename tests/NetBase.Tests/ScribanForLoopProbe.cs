using Scriban;
using Scriban.Runtime;
using Xunit;

public class ScribanForLoopProbe
{
    private static ScriptObject MakeModel()
    {
        var so = new ScriptObject();
        so["items"] = new ScriptArray { "a", "b" };
        return so;
    }

    [Fact]
    public void StandardSyntax_PureDoubleBrace()
    {
        var tpl = Template.Parse("{{ for x in items }}<{{ x }}>{{ end }}");
        Assert.False(tpl.HasErrors, string.Join("; ", tpl.Messages));
        Assert.Equal("<a><b>", tpl.Render(MakeModel()));
    }

}
