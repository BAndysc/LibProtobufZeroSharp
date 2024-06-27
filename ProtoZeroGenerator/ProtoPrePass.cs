using System.Collections.Generic;

namespace ProtoZeroGenerator;

internal class ProtoPrePass : Protobuf3BaseVisitor<Unit>
{
    public List<string> Enums { get; } = new List<string>();

    public List<string> Imports { get; } = new List<string>();

    public string Package { get; private set; }

    public string Namespace { get; private set; }

    public override Unit VisitImportStatement(Protobuf3Parser.ImportStatementContext context)
    {
        Imports.Add(context.strLit().GetText().Trim('"', '\''));
        return base.VisitImportStatement(context);
    }

    public override Unit VisitPackageStatement(Protobuf3Parser.PackageStatementContext context)
    {
        Package = context.fullIdent().GetText();
        return base.VisitPackageStatement(context);
    }

    public override Unit VisitOptionStatement(Protobuf3Parser.OptionStatementContext context)
    {
        if (context.optionName().GetText() == "csharp_namespace")
        {
            Namespace = context.constant().GetText().Trim('"', '\'');
        }

        return base.VisitOptionStatement(context);
    }

    public override Unit VisitEnumDef(Protobuf3Parser.EnumDefContext context)
    {
        Enums.Add(context.enumName().GetText());
        return base.VisitEnumDef(context);
    }
}