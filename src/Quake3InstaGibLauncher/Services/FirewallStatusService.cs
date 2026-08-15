namespace Quake3InstaGibLauncher.Services;

/// <summary>
/// Verifica (in SOLA LETTURA, non modifica mai nulla) se esiste gia' una regola del Firewall di
/// Windows che consente le connessioni in entrata per l'eseguibile di ioquake3, usando l'API
/// COM ufficiale del firewall (HNetCfg.FwPolicy2 / INetFwPolicy2 - la stessa usata dal pannello
/// "Windows Defender Firewall" di sistema). Non usa "netsh" perche' il suo output testuale e'
/// localizzato (cambia con la lingua di Windows) ed e' quindi inaffidabile da interpretare in modo
/// consistente su ogni PC. Usato per evitare di dire all'utente di fare un passo che ha gia' fatto:
/// prima non c'era alcun controllo reale, il launcher mostrava sempre lo stesso promemoria statico
/// anche quando l'eccezione era gia' presente in Windows.
/// </summary>
public static class FirewallStatusService
{
    // NET_FW_RULE_DIRECTION_IN = 1 (documentato nell'header Windows "netfw.h").
    private const int NetFwRuleDirIn = 1;

    /// <summary>True se esiste una regola attiva che consente il traffico IN ENTRATA per questo
    /// eseguibile. False se non esiste nessuna regola simile. Null se non e' stato possibile
    /// interrogare il firewall (es. servizio Windows Firewall disattivato, permessi insufficienti,
    /// COM non disponibile): in quel caso il chiamante deve trattarlo come "sconosciuto", mai come
    /// "non consentito", per non mostrare un avviso fuorviante.</summary>
    public static bool? IsExecutableAllowedInbound(string executablePath)
    {
        if (string.IsNullOrWhiteSpace(executablePath)) return null;

        try
        {
            var policyType = Type.GetTypeFromProgID("HNetCfg.FwPolicy2");
            if (policyType is null) return null;

            dynamic? policy = Activator.CreateInstance(policyType);
            if (policy is null) return null;

            foreach (var ruleObj in policy.Rules)
            {
                dynamic rule = ruleObj;
                try
                {
                    string? appName = rule.ApplicationName;
                    if (string.IsNullOrWhiteSpace(appName)) continue;
                    if (!string.Equals(appName, executablePath, StringComparison.OrdinalIgnoreCase)) continue;

                    bool enabled = rule.Enabled;
                    int direction = (int)rule.Direction;
                    if (enabled && direction == NetFwRuleDirIn)
                        return true;
                }
                catch
                {
                    // Singola regola con proprieta' non leggibili (rare, regole di sistema
                    // particolari): si ignora e si continua con le altre, non e' un errore fatale.
                }
            }

            return false;
        }
        catch
        {
            // Componente COM del firewall non disponibile o servizio Windows Firewall disattivato:
            // non deve mai far crashare la UI, semplicemente non sappiamo rispondere.
            return null;
        }
    }
}
