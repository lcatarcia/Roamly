using System.Globalization;
using System.Text.RegularExpressions;
using Roamly.Domain.Enums;

namespace Roamly.Api.Features.Campers.ParseCamperManual;

/// <summary>Interpreta le pagine estratte da un manuale PDF o scheda tecnica in una bozza strutturata.</summary>
public static partial class CamperManualParser
{
    private sealed record KnownSpecDefinition(
        string Id,
        string CanonicalName,
        string[] Aliases,
        string Zone,
        string View,
        int PositionX,
        int PositionY,
        bool AllowNonNumericValue = false);

    private static readonly KnownSpecDefinition[] KnownSpecs =
    [
        new(
            "ext-length",
            "Lunghezza esterna",
            ["Lunghezza esterna", "Lunghezza totale", "Lunghezza fuori tutto", "Lunghezza"],
            "Ingombri esterni",
            "side",
            50,
            88),
        new(
            "ext-width",
            "Larghezza esterna",
            ["Larghezza esterna", "Larghezza totale", "Larghezza fuori tutto", "Larghezza"],
            "Ingombri esterni",
            "front",
            50,
            88),
        new(
            "ext-height",
            "Altezza esterna",
            ["Altezza esterna", "Altezza totale", "Altezza fuori tutto", "Altezza complessiva"],
            "Ingombri esterni",
            "side",
            15,
            32),
        new(
            "wheelbase",
            "Passo",
            ["Passo", "Interasse"],
            "Ingombri esterni",
            "side",
            52,
            78),
        new(
            "int-height",
            "Altezza interna",
            ["Altezza interna", "Altezza utile interna", "Altezza abitacolo"],
            "Zona giorno / Dinette",
            "side",
            48,
            48),
        new(
            "rear-bed",
            "Letto posteriore",
            ["Letto posteriore", "Letto matrimoniale posteriore", "Letto trasversale", "Dimensioni letto posteriore"],
            "Zona notte",
            "top",
            22,
            50),
        new(
            "dinette-bed",
            "Letto dinette / anteriore",
            ["Letto dinette", "Letto anteriore", "Letto supplementare"],
            "Zona giorno / Dinette",
            "top",
            62,
            66),
        new(
            "drop-down-bed",
            "Letto basculante",
            ["Letto basculante", "Letto mansarda"],
            "Zona notte",
            "side",
            66,
            30),
        new(
            "fridge",
            "Frigorifero",
            ["Frigorifero", "Capacità frigorifero", "Frigorifero e vano"],
            "Cucina",
            "top",
            52,
            32),
        new(
            "fresh-water",
            "Acqua pulita",
            ["Acqua pulita", "Acque chiare", "Serbatoio acqua potabile", "Serbatoio acqua pulita", "Acqua potabile"],
            "Gavone / Impianti",
            "side",
            28,
            66),
        new(
            "grey-water",
            "Acque grigie",
            ["Acque grigie", "Serbatoio acque grigie", "Acqua di scarico", "Serbatoio recupero"],
            "Gavone / Impianti",
            "side",
            44,
            74),
        new(
            "fuel-tank",
            "Serbatoio carburante",
            ["Serbatoio carburante", "Serbatoio gasolio", "Capacità carburante"],
            "Cabina di guida",
            "side",
            72,
            70),
        new(
            "gas-compartment",
            "Vano bombole / gas",
            ["Vano bombole / regolatore", "Vano bombole", "Bombole gas", "Capacità gas"],
            "Gavone / Impianti",
            "side",
            16,
            72),
        new(
            "heating",
            "Riscaldamento",
            ["Riscaldamento", "Stufa / boiler", "Sistema di riscaldamento"],
            "Gavone / Impianti",
            "top",
            38,
            68,
            AllowNonNumericValue: true),
        new(
            "kerb-weight",
            "Massa in ordine di marcia",
            ["Massa in ordine di marcia", "Peso a vuoto", "Tara"],
            "Ingombri esterni",
            "front",
            26,
            74),
        new(
            "max-weight",
            "Massa complessiva",
            ["Massa complessiva", "Massa massima", "Peso massimo", "Massa tecnicamente ammissibile"],
            "Ingombri esterni",
            "front",
            74,
            74),
        new(
            "sliding-door",
            "Porta scorrevole",
            ["Porta scorrevole", "Porta cellula", "Luce porta scorrevole"],
            "Ingombri esterni",
            "side",
            62,
            54),
        new(
            "rear-doors",
            "Portelloni posteriori",
            ["Portelloni posteriori", "Apertura posteriore", "Luce portelloni posteriori"],
            "Ingombri esterni",
            "side",
            12,
            52),
        new(
            "kitchen-top",
            "Piano cucina",
            ["Piano cucina", "Blocco cucina", "Lavello e piano cottura"],
            "Cucina",
            "top",
            56,
            26),
        new(
            "shower-tray",
            "Piatto doccia / bagno",
            ["Piatto doccia", "Porta / parete bagno", "Vano bagno"],
            "Bagno / Doccia",
            "top",
            43,
            68),
        new(
            "underbed-storage",
            "Vani sottoletto / gavone",
            ["Vani sottoletto", "Gavone posteriore", "Gavone"],
            "Gavone / Impianti",
            "side",
            20,
            64),
        new(
            "central-aisle",
            "Passaggio centrale",
            ["Passaggio centrale", "Corridoio centrale"],
            "Zona giorno / Dinette",
            "top",
            50,
            50),
        new(
            "overhead-lockers",
            "Pensili",
            ["Pensili", "Pensili superiori"],
            "Zona giorno / Dinette",
            "side",
            58,
            28),
        new(
            "battery-compartment",
            "Vano batteria / fusibili",
            ["Vano batteria / fusibili", "Batteria servizi"],
            "Gavone / Impianti",
            "top",
            78,
            35),
    ];

    private static readonly string[] KnownBrands =
    [
        "Adria",
        "Laika",
        "Hymer",
        "Bürstner",
        "Burstner",
        "Carthago",
        "Knaus",
        "Weinsberg",
        "Chausson",
        "Challenger",
        "Roller Team",
        "McLouis",
        "Mobilvetta",
        "Arca",
        "Elnagh",
        "Rimor",
        "Giottiline",
        "Benimar",
        "Rapido",
        "Pössl",
        "Possl",
        "Globecar",
        "Westfalia",
        "Dethleffs",
        "Sunlight",
        "Carado",
        "Etrusco",
        "Font Vendôme",
        "Pilote",
        "Malibu",
        "Frankia",
    ];

    /// <summary>Analizza le pagine estratte e produce i valori candidati del profilo e la mappa delle misure.</summary>
    public static CamperManualParseResponse Parse(
        IReadOnlyList<ExtractedManualPage> pages,
        string? requestedSourceName,
        string? requestedSourceUrl)
    {
        ArgumentNullException.ThrowIfNull(pages);

        var allLines = pages
            .SelectMany(page => page.Lines.Select(line => (page.PageNumber, Line: line)))
            .ToArray();
        var fullText = string.Join("\n", allLines.Select(item => item.Line));

        var (brand, model) = DetectBrandAndModel(allLines);
        var year = DetectYear(fullText);
        var vehicleKind = DetectVehicleKind(fullText, model);
        var fuelKind = DetectFuelKind(fullText);
        var detectedUrl = NormalizeUrl(requestedSourceUrl) ?? DetectFirstReferenceUrl(fullText);
        var sourceName = ResolveSourceName(requestedSourceName, brand, model, year, detectedUrl);

        var measurements = ExtractMeasurements(pages);

        var lengthMm = FindDimensionMm(measurements, "ext-length") ?? ExtractInlineMillimeters(fullText, "Lunghezza esterna");
        var widthMm = FindDimensionMm(measurements, "ext-width") ?? ExtractInlineMillimeters(fullText, "Larghezza esterna");
        var heightMm = FindDimensionMm(measurements, "ext-height") ?? ExtractInlineMillimeters(fullText, "Altezza esterna");
        var kerbWeightKg = FindIntegerWithUnit(measurements, "kerb-weight", "kg");
        var maxWeightKg = FindIntegerWithUnit(measurements, "max-weight", "kg");
        var freshWaterL = FindIntegerWithUnit(measurements, "fresh-water", "l");
        var greyWaterL = FindIntegerWithUnit(measurements, "grey-water", "l");
        var fuelTankL = FindIntegerWithUnit(measurements, "fuel-tank", "l");
        var gasKg = FindIntegerWithUnit(measurements, "gas-compartment", "kg");
        var consumption = ExtractConsumption(fullText);

        var warnings = BuildWarnings(
            lengthMm,
            widthMm,
            heightMm,
            kerbWeightKg,
            maxWeightKg,
            freshWaterL,
            greyWaterL,
            fuelTankL,
            gasKg,
            measurements);

        return new CamperManualParseResponse(
            sourceName,
            detectedUrl,
            brand,
            model,
            year,
            vehicleKind.HasValue ? (int)vehicleKind.Value : null,
            fuelKind.HasValue ? (int)fuelKind.Value : null,
            lengthMm,
            widthMm,
            heightMm,
            kerbWeightKg,
            maxWeightKg,
            freshWaterL,
            greyWaterL,
            fuelTankL,
            gasKg,
            consumption,
            measurements,
            warnings);
    }

    private static List<CamperManualMeasurementDto> ExtractMeasurements(IReadOnlyList<ExtractedManualPage> pages)
    {
        var results = new List<CamperManualMeasurementDto>();
        var seenIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var page in pages)
        {
            var lines = page.Lines;
            for (var i = 0; i < lines.Count; i++)
            {
                var line = lines[i];

                // 1. Riga singola "Etichetta: Valore" o "Etichetta — Valore"
                var inlineSplit = InlineKeyValueRegex().Match(line);
                if (inlineSplit.Success)
                {
                    var keyCandidate = inlineSplit.Groups[1].Value.Trim();
                    var valueCandidate = inlineSplit.Groups[2].Value.Trim();
                    var knownInline = MatchKnownSpec(keyCandidate);
                    if (knownInline is not null
                        && IsPlausibleValue(valueCandidate, knownInline.AllowNonNumericValue)
                        && seenIds.Add(knownInline.Id))
                    {
                        results.Add(new CamperManualMeasurementDto(
                            knownInline.Id,
                            knownInline.CanonicalName,
                            valueCandidate,
                            knownInline.Zone,
                            knownInline.View,
                            knownInline.PositionX,
                            knownInline.PositionY,
                            null,
                            page.PageNumber));
                        continue;
                    }
                }

                // 2. Cella di tabella su righe consecutive: [Etichetta] -> [Valore] -> [Nota opzionale]
                var known = MatchKnownSpec(line);
                if (known is null || seenIds.Contains(known.Id) || i + 1 >= lines.Count)
                {
                    continue;
                }

                var nextLine = lines[i + 1].Trim();
                if (!IsPlausibleValue(nextLine, known.AllowNonNumericValue))
                {
                    continue;
                }

                string? description = null;
                var consumed = 1;
                if (i + 2 < lines.Count && IsPlausibleNote(lines[i + 2]))
                {
                    description = lines[i + 2].Trim();
                    consumed = 2;

                    if (!description.EndsWith('.')
                        && i + 3 < lines.Count
                        && IsPlausibleNoteContinuation(lines[i + 3]))
                    {
                        description = description + " " + lines[i + 3].Trim();
                        consumed = 3;
                    }
                }

                seenIds.Add(known.Id);
                results.Add(new CamperManualMeasurementDto(
                    known.Id,
                    known.CanonicalName,
                    nextLine,
                    known.Zone,
                    known.View,
                    known.PositionX,
                    known.PositionY,
                    description,
                    page.PageNumber));
                i += consumed;
            }
        }

        return results;
    }

    private static KnownSpecDefinition? MatchKnownSpec(string candidate)
    {
        var cleaned = candidate.Trim('•', '-', '–', '—', ':', ' ', '\t');
        if (cleaned.Length is < 3 or > 60)
        {
            return null;
        }

        foreach (var spec in KnownSpecs)
        {
            foreach (var alias in spec.Aliases)
            {
                if (string.Equals(cleaned, alias, StringComparison.OrdinalIgnoreCase))
                {
                    return spec;
                }
            }
        }

        return null;
    }

    private static bool IsPlausibleValue(string candidate, bool allowNonNumeric)
    {
        if (string.IsNullOrWhiteSpace(candidate) || candidate.Length > 80)
        {
            return false;
        }

        if (MatchKnownSpec(candidate) is not null)
        {
            return false;
        }

        if (candidate.Equals("Riferimento", StringComparison.OrdinalIgnoreCase)
            || candidate.Equals("Nota", StringComparison.OrdinalIgnoreCase)
            || candidate.Equals("Elemento", StringComparison.OrdinalIgnoreCase)
            || candidate.Equals("Larghezza", StringComparison.OrdinalIgnoreCase)
            || candidate.Equals("Altezza", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        if (MeasurementValueRegex().IsMatch(candidate))
        {
            return true;
        }

        return allowNonNumeric && candidate.Length <= 45 && !candidate.EndsWith('.');
    }

    private static bool IsPlausibleNote(string candidate)
    {
        if (string.IsNullOrWhiteSpace(candidate) || candidate.Length > 180)
        {
            return false;
        }

        if (MatchKnownSpec(candidate) is not null || PageOrHeadingRegex().IsMatch(candidate))
        {
            return false;
        }

        return !MeasurementOnlyValueRegex().IsMatch(candidate);
    }

    private static bool IsPlausibleNoteContinuation(string candidate)
    {
        if (string.IsNullOrWhiteSpace(candidate) || candidate.Length > 80)
        {
            return false;
        }

        if (MatchKnownSpec(candidate) is not null || PageOrHeadingRegex().IsMatch(candidate))
        {
            return false;
        }

        return candidate.EndsWith('.');
    }

    private static (string? Brand, string? Model) DetectBrandAndModel(
        IReadOnlyList<(int PageNumber, string Line)> lines)
    {
        foreach (var (_, line) in lines.Take(40))
        {
            var cleaned = line.Split(['•', '|', '—'])[0].Trim();
            foreach (var brand in KnownBrands)
            {
                if (cleaned.StartsWith(brand + " ", StringComparison.OrdinalIgnoreCase)
                    && cleaned.Length <= 60)
                {
                    var rawModel = cleaned[(brand.Length + 1)..].Trim();
                    if (!string.IsNullOrWhiteSpace(rawModel) && rawModel.Length <= 60)
                    {
                        return (NormalizeBrandCase(brand), NormalizeModelCase(rawModel));
                    }
                }
            }
        }

        foreach (var (_, line) in lines)
        {
            foreach (var brand in KnownBrands)
            {
                var idx = line.IndexOf(brand + " ", StringComparison.OrdinalIgnoreCase);
                if (idx >= 0)
                {
                    var after = line[(idx + brand.Length + 1)..];
                    var stop = after.IndexOfAny([',', '.', ';', '•', '(', '\r', '\n']);
                    var candidate = (stop >= 0 ? after[..stop] : after).Trim();
                    var immatricolatoIndex = candidate.IndexOf(" immatricolat", StringComparison.OrdinalIgnoreCase);
                    if (immatricolatoIndex > 0)
                    {
                        candidate = candidate[..immatricolatoIndex].Trim();
                    }

                    if (candidate.Length is >= 2 and <= 60)
                    {
                        return (NormalizeBrandCase(brand), NormalizeModelCase(candidate));
                    }
                }
            }
        }

        return (null, null);
    }

    private static string NormalizeBrandCase(string brand)
    {
        return brand.Equals("Burstner", StringComparison.OrdinalIgnoreCase) ? "Bürstner"
            : brand.Equals("Possl", StringComparison.OrdinalIgnoreCase) ? "Pössl"
            : brand;
    }

    private static string NormalizeModelCase(string rawModel)
    {
        var tokens = rawModel.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        for (var i = 0; i < tokens.Length; i++)
        {
            var token = tokens[i];
            if (token.Length <= 3 && token.All(char.IsLetterOrDigit))
            {
                tokens[i] = token.ToUpperInvariant();
            }
            else if (token.All(char.IsUpper) && token.Length > 3)
            {
                tokens[i] = char.ToUpperInvariant(token[0]) + token[1..].ToLowerInvariant();
            }
        }

        return string.Join(' ', tokens);
    }

    private static short? DetectYear(string fullText)
    {
        var match = YearContextRegex().Match(fullText);
        if (match.Success
            && short.TryParse(match.Groups[1].Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var year)
            && year is >= 1970 and <= 2100)
        {
            return year;
        }

        return null;
    }

    private static VehicleKind? DetectVehicleKind(string fullText, string? model)
    {
        if (fullText.Contains("mansardato", StringComparison.OrdinalIgnoreCase))
        {
            return VehicleKind.Alcove;
        }

        if (fullText.Contains("motorhome", StringComparison.OrdinalIgnoreCase)
            || fullText.Contains("integrale", StringComparison.OrdinalIgnoreCase))
        {
            return VehicleKind.Motorhome;
        }

        if (fullText.Contains("campervan", StringComparison.OrdinalIgnoreCase)
            || fullText.Contains("camper van", StringComparison.OrdinalIgnoreCase)
            || fullText.Contains("furgonato", StringComparison.OrdinalIgnoreCase)
            || fullText.Contains("Jumper", StringComparison.OrdinalIgnoreCase)
            || (model?.Contains("Twin", StringComparison.OrdinalIgnoreCase) ?? false))
        {
            return VehicleKind.Van;
        }

        if (fullText.Contains("semintegrale", StringComparison.OrdinalIgnoreCase)
            || fullText.Contains("profilato", StringComparison.OrdinalIgnoreCase))
        {
            return VehicleKind.Coachbuilt;
        }

        return null;
    }

    private static FuelKind? DetectFuelKind(string fullText)
    {
        if (fullText.Contains("diesel", StringComparison.OrdinalIgnoreCase)
            || fullText.Contains("gasolio", StringComparison.OrdinalIgnoreCase)
            || fullText.Contains("BlueHDi", StringComparison.OrdinalIgnoreCase)
            || fullText.Contains("MultiJet", StringComparison.OrdinalIgnoreCase)
            || fullText.Contains("Jumper", StringComparison.OrdinalIgnoreCase)
            || fullText.Contains("Ducato", StringComparison.OrdinalIgnoreCase))
        {
            return FuelKind.Diesel;
        }

        if (fullText.Contains("elettrico", StringComparison.OrdinalIgnoreCase))
        {
            return FuelKind.Electric;
        }

        if (fullText.Contains("ibrido", StringComparison.OrdinalIgnoreCase))
        {
            return FuelKind.Hybrid;
        }

        if (fullText.Contains("benzina", StringComparison.OrdinalIgnoreCase))
        {
            return FuelKind.Petrol;
        }

        return null;
    }

    private static string? DetectFirstReferenceUrl(string fullText)
    {
        var matches = HttpsUrlRegex().Matches(fullText);
        foreach (Match match in matches)
        {
            var candidate = match.Value.TrimEnd('.', ',', ';', ')', ']');
            if (Uri.TryCreate(candidate, UriKind.Absolute, out var uri)
                && uri.Scheme == Uri.UriSchemeHttps)
            {
                return uri.AbsoluteUri;
            }
        }

        return null;
    }

    private static string? NormalizeUrl(string? url)
    {
        if (string.IsNullOrWhiteSpace(url))
        {
            return null;
        }

        return Uri.TryCreate(url.Trim(), UriKind.Absolute, out var uri)
            ? uri.AbsoluteUri
            : null;
    }

    private static string ResolveSourceName(
        string? requestedSourceName,
        string? brand,
        string? model,
        short? year,
        string? detectedUrl)
    {
        if (!string.IsNullOrWhiteSpace(requestedSourceName))
        {
            return requestedSourceName.Trim();
        }

        if (!string.IsNullOrWhiteSpace(brand) && !string.IsNullOrWhiteSpace(model))
        {
            return year.HasValue
                ? $"Manuale {brand} {model} ({year.Value})"
                : $"Manuale {brand} {model}";
        }

        if (!string.IsNullOrWhiteSpace(detectedUrl)
            && Uri.TryCreate(detectedUrl, UriKind.Absolute, out var uri))
        {
            return $"Scheda tecnica ({uri.Host})";
        }

        return "Manuale tecnico camper";
    }

    private static int? FindDimensionMm(List<CamperManualMeasurementDto> measurements, string id)
    {
        var item = measurements.Find(m => string.Equals(m.Id, id, StringComparison.OrdinalIgnoreCase));
        return item is null ? null : ParseFirstMillimeters(item.Value);
    }

    private static int? ExtractInlineMillimeters(string fullText, string label)
    {
        var idx = fullText.IndexOf(label, StringComparison.OrdinalIgnoreCase);
        if (idx < 0)
        {
            return null;
        }

        var snippet = fullText.Substring(idx, Math.Min(120, fullText.Length - idx));
        return ParseFirstMillimeters(snippet);
    }

    private static int? ParseFirstMillimeters(string text)
    {
        var mmMatch = MillimeterValueRegex().Match(text);
        if (mmMatch.Success)
        {
            var digits = mmMatch.Groups[1].Value.Replace(".", string.Empty, StringComparison.Ordinal)
                .Replace(" ", string.Empty, StringComparison.Ordinal);
            if (int.TryParse(digits, NumberStyles.Integer, CultureInfo.InvariantCulture, out var mm)
                && mm is >= 200 and <= 25_000)
            {
                return mm;
            }
        }

        var cmMatch = CentimeterValueRegex().Match(text);
        if (cmMatch.Success
            && decimal.TryParse(
                cmMatch.Groups[1].Value.Replace(',', '.'),
                NumberStyles.Number,
                CultureInfo.InvariantCulture,
                out var cm))
        {
            var mm = (int)Math.Round(cm * 10m);
            if (mm is >= 200 and <= 25_000)
            {
                return mm;
            }
        }

        return null;
    }

    private static int? FindIntegerWithUnit(
        List<CamperManualMeasurementDto> measurements,
        string id,
        string expectedUnit)
    {
        var item = measurements.Find(m => string.Equals(m.Id, id, StringComparison.OrdinalIgnoreCase));
        if (item is null)
        {
            return null;
        }

        var match = UnitIntegerRegex().Match(item.Value);
        if (!match.Success)
        {
            return null;
        }

        var unit = match.Groups[2].Value;
        if (!unit.Equals(expectedUnit, StringComparison.OrdinalIgnoreCase)
            && !(expectedUnit.Equals("l", StringComparison.OrdinalIgnoreCase)
                && unit.StartsWith("litr", StringComparison.OrdinalIgnoreCase)))
        {
            return null;
        }

        var digits = match.Groups[1].Value.Replace(".", string.Empty, StringComparison.Ordinal);
        return int.TryParse(digits, NumberStyles.Integer, CultureInfo.InvariantCulture, out var value) && value > 0
            ? value
            : null;
    }

    private static decimal? ExtractConsumption(string fullText)
    {
        var match = ConsumptionRegex().Match(fullText);
        if (match.Success
            && decimal.TryParse(
                match.Groups[1].Value.Replace(',', '.'),
                NumberStyles.Number,
                CultureInfo.InvariantCulture,
                out var value)
            && value is >= 0.1m and <= 99.9m)
        {
            return Math.Round(value, 1);
        }

        return null;
    }

    private static List<string> BuildWarnings(
        int? lengthMm,
        int? widthMm,
        int? heightMm,
        int? kerbWeightKg,
        int? maxWeightKg,
        int? freshWaterL,
        int? greyWaterL,
        int? fuelTankL,
        int? gasKg,
        List<CamperManualMeasurementDto> measurements)
    {
        var warnings = new List<string>();
        if (measurements.Count == 0
            && lengthMm is null
            && widthMm is null
            && heightMm is null
            && freshWaterL is null
            && greyWaterL is null)
        {
            warnings.Add("Non sono state individuate tabelle metriche standard: verifica che il PDF contenga testo selezionabile.");
        }

        if ((freshWaterL.HasValue || greyWaterL.HasValue) && (!fuelTankL.HasValue || !gasKg.HasValue))
        {
            warnings.Add("Il manuale riporta le capacità idriche (acqua pulita/grigie) ma non specifica serbatoio carburante o kg gas: per salvare il gruppo Capacità nel profilo principale puoi completare i valori mancanti oppure conservarli nella mappa delle misure.");
        }

        if (kerbWeightKg is null && maxWeightKg is null)
        {
            warnings.Add("Le masse (in ordine di marcia e massima) non sono dichiarate nel documento e dipendono dall'omologazione sul libretto.");
        }

        return warnings;
    }

    [GeneratedRegex(@"^([^:–—]{3,50})\s*[:—]\s*(.{2,80})$")]
    private static partial Regex InlineKeyValueRegex();

    [GeneratedRegex(@"\b\d[\d.,\s×xX–-]*\s*(?:mm|cm|m|kg|L|l|litri|kW|Ah|W|bar)\b")]
    private static partial Regex MeasurementValueRegex();

    [GeneratedRegex(@"^\d[\d.,\s×xX–-]*\s*(?:mm|cm|m|kg|L|l|litri)$")]
    private static partial Regex MeasurementOnlyValueRegex();

    [GeneratedRegex(@"^(?:Pagina\s+\d+|\d+(?:\.\d+)?\s+[A-ZÀ-Ü]|ADRIA\s+TWIN)", RegexOptions.IgnoreCase)]
    private static partial Regex PageOrHeadingRegex();

    [GeneratedRegex(@"(?:immatricolazione|immatricolato(?:\s+nel)?|anno|model\s+year)\s*(19\d{2}|20\d{2})", RegexOptions.IgnoreCase)]
    private static partial Regex YearContextRegex();

    [GeneratedRegex(@"https://[^\s""'<>]+", RegexOptions.IgnoreCase)]
    private static partial Regex HttpsUrlRegex();

    [GeneratedRegex(@"(\d{1,2}(?:\.\d{3})+|\d{3,5})\s*(?:[–-]\s*(?:\d{1,2}(?:\.\d{3})+|\d{3,5})\s*)?mm\b", RegexOptions.IgnoreCase)]
    private static partial Regex MillimeterValueRegex();

    [GeneratedRegex(@"(\d{2,4}(?:[.,]\d{1,2})?)\s*cm\b", RegexOptions.IgnoreCase)]
    private static partial Regex CentimeterValueRegex();

    [GeneratedRegex(@"(\d{1,2}(?:\.\d{3})+|\d{1,4})\s*(kg|L|l|litri)\b", RegexOptions.IgnoreCase)]
    private static partial Regex UnitIntegerRegex();

    [GeneratedRegex(@"(\d{1,2}(?:[.,]\d)?)\s*l\s*/\s*100\s*km", RegexOptions.IgnoreCase)]
    private static partial Regex ConsumptionRegex();
}
