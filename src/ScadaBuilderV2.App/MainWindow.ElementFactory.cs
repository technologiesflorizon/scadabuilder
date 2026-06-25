using ScadaBuilderV2.Domain.Elements;
using ScadaBuilderV2.Domain.Scenes;

namespace ScadaBuilderV2.App;

// Element+ creation, insert-kind parsing, command-id mapping, and shape/button
// naming helpers. Extracted from MainWindow.xaml.cs as a behavior-preserving split;
// instance members (CreateModernElement, ResetElementSequences) remain part of the
// MainWindow partial class and use editor sequence/state fields from the main file.
public partial class MainWindow
{
    private static ScadaElementKind? ParseInsertKind(string? kind)
    {
        return Enum.TryParse<ScadaElementKind>(kind, ignoreCase: true, out var parsed) &&
            (parsed == ScadaElementKind.Text ||
                parsed == ScadaElementKind.InputText ||
                parsed == ScadaElementKind.InputNumeric ||
                parsed == ScadaElementKind.Shape ||
                parsed == ScadaElementKind.Button)
                ? parsed
                : null;
    }

    private static ScadaShapeKind? ParseShapeKind(string? shapeKind)
    {
        return Enum.TryParse<ScadaShapeKind>(shapeKind, ignoreCase: true, out var parsed)
            ? parsed
            : null;
    }

    private static bool IsTwoPointShape(ScadaShapeKind shapeKind)
    {
        return shapeKind is ScadaShapeKind.Line or ScadaShapeKind.Arrow;
    }

    private static string? CommandIdForElementKind(ScadaElementKind kind)
    {
        return kind switch
        {
            ScadaElementKind.Text => "insert.text",
            ScadaElementKind.InputText => "insert.input-text",
            ScadaElementKind.InputNumeric => "insert.input-numeric",
            _ => null
        };
    }

    private static string? CommandIdForShape(ScadaShapeKind shapeKind)
    {
        return shapeKind switch
        {
            ScadaShapeKind.Rectangle => "insert.shape.rectangle",
            ScadaShapeKind.Ellipse => "insert.shape.ellipse",
            ScadaShapeKind.Circle => "insert.shape.circle",
            ScadaShapeKind.Triangle => "insert.shape.triangle",
            ScadaShapeKind.Star => "insert.shape.star",
            ScadaShapeKind.Line => "insert.shape.line",
            ScadaShapeKind.Arrow => "insert.shape.arrow",
            ScadaShapeKind.IndicatorLamp => "insert.hmi.indicator-lamp",
            ScadaShapeKind.HorizontalBar => "insert.hmi.bar-horizontal",
            ScadaShapeKind.VerticalBar => "insert.hmi.bar-vertical",
            ScadaShapeKind.Tank => "insert.hmi.tank",
            ScadaShapeKind.PipeHorizontal => "insert.hmi.pipe-horizontal",
            ScadaShapeKind.PipeVertical => "insert.hmi.pipe-vertical",
            ScadaShapeKind.Valve => "insert.hmi.valve",
            ScadaShapeKind.Pump => "insert.hmi.pump",
            ScadaShapeKind.Motor => "insert.hmi.motor",
            ScadaShapeKind.Fan => "insert.hmi.fan",
            ScadaShapeKind.Conveyor => "insert.hmi.conveyor",
            ScadaShapeKind.Gauge => "insert.hmi.gauge",
            ScadaShapeKind.Switch => "insert.hmi.switch",
            ScadaShapeKind.Breaker => "insert.hmi.breaker",
            ScadaShapeKind.Transformer => "insert.hmi.transformer",
            ScadaShapeKind.AlarmBeacon => "insert.hmi.alarm-beacon",
            _ => null
        };
    }

    private static string? CommandIdForButton(ScadaButtonKind buttonKind)
    {
        return buttonKind switch
        {
            ScadaButtonKind.Command => "insert.button.command",
            ScadaButtonKind.Toggle => "insert.button.toggle",
            ScadaButtonKind.Navigation => "insert.button.navigation",
            ScadaButtonKind.AlarmAcknowledge => "insert.button.alarm-ack",
            ScadaButtonKind.EmergencyStop => "insert.button.emergency-stop",
            _ => null
        };
    }

    private ScadaElement CreateModernElement(ScadaElementKind kind, double x, double y, ScadaShapeKind? shapeKindOverride = null)
    {
        if (kind == ScadaElementKind.Text)
        {
            var sequence = _nextTextSequence++;
            var id = CreateUniqueElementId($"text_{sequence:000}");
            return ScadaElement.CreateText(id, $"Text{sequence:000}", x, y);
        }

        if (kind == ScadaElementKind.InputNumeric)
        {
            var sequence = _nextInputNumericSequence++;
            var id = CreateUniqueElementId($"input_numeric_{sequence:000}");
            return ScadaElement.CreateInputNumeric(id, $"InputNumeric{sequence:000}", x, y);
        }

        if (kind == ScadaElementKind.Shape)
        {
            var sequence = _nextShapeSequence++;
            var shapeKind = shapeKindOverride ?? _pendingInsertShapeKind ?? ScadaShapeKind.Rectangle;
            var id = CreateUniqueElementId($"shape_{sequence:000}");
            return ScadaElement.CreateShape(id, $"{FormatShapeName(shapeKind)}{sequence:000}", shapeKind, x, y);
        }

        if (kind == ScadaElementKind.Button)
        {
            var sequence = _nextButtonSequence++;
            var buttonKind = _pendingInsertButtonKind ?? ScadaButtonKind.Command;
            var id = CreateUniqueElementId($"button_{sequence:000}");
            return ScadaElement.CreateButton(id, $"{FormatButtonName(buttonKind)}{sequence:000}", x, y, buttonKind);
        }

        var textSequence = _nextInputTextSequence++;
        var inputTextId = CreateUniqueElementId($"input_text_{textSequence:000}");
        return ScadaElement.CreateInputText(inputTextId, $"InputText{textSequence:000}", x, y);
    }

    private static string FormatShapeName(ScadaShapeKind shapeKind)
    {
        return shapeKind switch
        {
            ScadaShapeKind.RoundedRectangle => "RectangleArrondi",
            ScadaShapeKind.Ellipse => "Ellipse",
            ScadaShapeKind.Circle => "Cercle",
            ScadaShapeKind.Triangle => "Triangle",
            ScadaShapeKind.Star => "Etoile",
            ScadaShapeKind.Line => "Ligne",
            ScadaShapeKind.Arrow => "Fleche",
            ScadaShapeKind.IndicatorLamp => "Voyant",
            ScadaShapeKind.HorizontalBar => "BarreHorizontale",
            ScadaShapeKind.VerticalBar => "BarreVerticale",
            ScadaShapeKind.Tank => "Reservoir",
            ScadaShapeKind.PipeHorizontal => "TuyauHorizontal",
            ScadaShapeKind.PipeVertical => "TuyauVertical",
            ScadaShapeKind.Valve => "Vanne",
            ScadaShapeKind.Pump => "Pompe",
            ScadaShapeKind.Motor => "Moteur",
            ScadaShapeKind.Fan => "Ventilateur",
            ScadaShapeKind.Conveyor => "Convoyeur",
            ScadaShapeKind.Gauge => "Jauge",
            ScadaShapeKind.Switch => "Interrupteur",
            ScadaShapeKind.Breaker => "Disjoncteur",
            ScadaShapeKind.Transformer => "Transformateur",
            ScadaShapeKind.AlarmBeacon => "BaliseAlarme",
            _ => "Rectangle"
        };
    }

    private static string FormatShapeLabel(ScadaShapeKind shapeKind)
    {
        return shapeKind switch
        {
            ScadaShapeKind.RoundedRectangle => "rectangle arrondi",
            ScadaShapeKind.Ellipse => "ellipse",
            ScadaShapeKind.Circle => "cercle",
            ScadaShapeKind.Triangle => "triangle",
            ScadaShapeKind.Star => "etoile",
            ScadaShapeKind.Line => "ligne",
            ScadaShapeKind.Arrow => "fleche",
            ScadaShapeKind.IndicatorLamp => "voyant HMI",
            ScadaShapeKind.HorizontalBar => "barre horizontale HMI",
            ScadaShapeKind.VerticalBar => "barre verticale HMI",
            ScadaShapeKind.Tank => "reservoir HMI",
            ScadaShapeKind.PipeHorizontal => "tuyau horizontal HMI",
            ScadaShapeKind.PipeVertical => "tuyau vertical HMI",
            ScadaShapeKind.Valve => "vanne HMI",
            ScadaShapeKind.Pump => "pompe HMI",
            ScadaShapeKind.Motor => "moteur HMI",
            ScadaShapeKind.Fan => "ventilateur HMI",
            ScadaShapeKind.Conveyor => "convoyeur HMI",
            ScadaShapeKind.Gauge => "jauge HMI",
            ScadaShapeKind.Switch => "interrupteur electrique HMI",
            ScadaShapeKind.Breaker => "disjoncteur HMI",
            ScadaShapeKind.Transformer => "transformateur HMI",
            ScadaShapeKind.AlarmBeacon => "balise alarme HMI",
            _ => "rectangle"
        };
    }

    private static string FormatButtonName(ScadaButtonKind buttonKind)
    {
        return buttonKind switch
        {
            ScadaButtonKind.Toggle => "Toggle",
            ScadaButtonKind.Navigation => "Navigation",
            ScadaButtonKind.AlarmAcknowledge => "Acquitter",
            ScadaButtonKind.EmergencyStop => "ArretUrgence",
            _ => "Bouton"
        };
    }

    private static string FormatButtonLabel(ScadaButtonKind buttonKind)
    {
        return buttonKind switch
        {
            ScadaButtonKind.Toggle => "bouton bascule",
            ScadaButtonKind.Navigation => "bouton navigation",
            ScadaButtonKind.AlarmAcknowledge => "bouton acquittement alarme",
            ScadaButtonKind.EmergencyStop => "bouton arret d'urgence",
            _ => "bouton"
        };
    }

    private void ResetElementSequences(ScadaScene scene)
    {
        var elements = FlattenElements(scene.Elements).ToArray();
        _nextTextSequence = elements.Count(element => element.Kind == ScadaElementKind.Text && !element.IsImportedFromLegacy) + 1;
        _nextInputTextSequence = elements.Count(element => element.Kind == ScadaElementKind.InputText) + 1;
        _nextInputNumericSequence = elements.Count(element => element.Kind == ScadaElementKind.InputNumeric) + 1;
        _nextShapeSequence = elements.Count(element => element.Kind == ScadaElementKind.Shape && !element.IsImportedFromLegacy) + 1;
        _nextButtonSequence = elements.Count(element => element.Kind == ScadaElementKind.Button && !element.IsImportedFromLegacy) + 1;
        _nextGroupSequence = elements.Count(element => element.Kind == ScadaElementKind.Group) + 1;
    }
}
