# Security baseline

## Wat is een kritieke bevinding?
- Secret scanning (Gitleaks): elk gevonden secret.
- SCA (Trivy): elke kwetsbaarheid met ernst CRITICAL of HIGH (CVSS >= 7.0).
- SAST (Codacy): elke nieuwe security-issue bovenop de baseline.

## Baselinebeleid
De starter-app bevat bewust kwetsbaarheden. De bevindingen uit de eerste scan
op de ongewijzigde app worden aanvaard en staan in:
- `.gitleaksignore` (Gitleaks)
- `.trivyignore` (Trivy)
- `max-allowed-issues` in `codacy.yml` (Codacy)

Elke aanvaarde bevinding heeft een reden. Elke nieuwe bevinding die niet in de
baseline staat, laat de pipeline falen, en dan wordt er niet gedeployd.

## Aanvaard in opdracht 3
- Codacy: +34 Sonarscharp-meldingen op `app/Program.cs` (bestandsheader,
  CLSCompliant, ComVisible, AssemblyVersion). Enkel stijl, geen
  beveiligingsimpact. `max-allowed-issues` van 4273 naar 4310.
