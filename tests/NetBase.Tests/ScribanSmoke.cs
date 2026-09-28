using NetBase.Model.Entities;
using NetBase.Service.Sys;
using Scriban;
using Xunit;

public class ScribanSmoke
{
    [Fact]
    public void Render_RealEntityTemplate_WithRealColumns()
    {
        var model = new GenModel();
        model.Table.FunctionName = "X功能";
        model.Table.TableName = "biz_x";
        model.Columns.Add(new SysGenTableColumn
        {
            ColumnName = "amount",
            PropertyName = "Amount",
            ColumnComment = "金额",
            CSharpType = "decimal",
            IsRequired = true
        });

        var build = typeof(SysGenTableService).GetMethod(
            "BuildTemplateModel",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
        var templateModel = build!.Invoke(null, new object?[] { model });

        var stream = typeof(SysGenTableService).Assembly.GetManifestResourceStream(
            "NetBase.Service.Templates.Gen.entity.sbn")!;
        string templateText;
        using (var reader = new StreamReader(stream)) templateText = reader.ReadToEnd();

        var tpl = Template.Parse(templateText);
        Assert.False(tpl.HasErrors, "模板解析失败: " + string.Join("; ", tpl.Messages));

        var result = tpl.Render(templateModel);
        System.IO.File.WriteAllText(@"E:\02AI\ZCode\Net通用基础框架\scriban-keys.txt", result ?? "(null)");
        Assert.Contains("Amount", result);
    }
}
