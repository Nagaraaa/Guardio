using GardeAudio.Core;

var time = DateTimeOffset.Parse("2026-10-20T12:00:00Z");
var h = new OutputDevice("headset", "Casque", true, false);
var s = new OutputDevice("speakers", "Enceintes", true, false);
var p = new OutputDevice("ps5", "DualSense", true, true);
var roles = Enum.GetValues<AudioRole>();
var results = new List<(string Name, bool Passed)>();
GuardPolicy Make(bool controllerPresent = false)
{
    var policy = new GuardPolicy { FallbackId = h.Id };
    policy.Initialize(new[] { h, s, p with { Active = controllerPresent } }, roles.ToDictionary(r => r, _ => (string?)h.Id));
    return policy;
}
void Test(string name, Func<bool> check)
{
    bool passed;
    try { passed = check(); } catch (Exception ex) { Console.WriteLine(ex); passed = false; }
    results.Add((name, passed)); Console.WriteLine($"{(passed ? "PASS" : "FAIL")} {name}");
}
Test("Un branchement rétablit la sortie précédente", () => { var g = Make(); g.ObserveDevices([h,s,p],time); return g.ObserveDefault(AudioRole.Multimedia,p.Id,time)?.TargetId == h.Id; });
Test("Une manette déjà connectée au lancement n’est pas verrouillée", () => Make(true).ObserveDefault(AudioRole.Multimedia,p.Id,time) is null);
Test("Chaque rôle est protégé séparément", () => { var g=Make(); g.ObserveDevices([h,s,p],time); return roles.All(r=>g.ObserveDefault(r,p.Id,time)?.Role==r); });
Test("Une même bascule n’est corrigée qu’une fois", () => { var g=Make(); g.ObserveDevices([h,s,p],time); var d=g.ObserveDefault(AudioRole.Console,p.Id,time)!; g.AcknowledgeRestore(d,true); return g.ObserveDefault(AudioRole.Console,p.Id,time.AddMilliseconds(100)) is null; });
Test("Un échec ne provoque pas une boucle de corrections", () => { var g=Make(); g.ObserveDevices([h,s,p],time); var d=g.ObserveDefault(AudioRole.Console,p.Id,time)!; g.AcknowledgeRestore(d,false); return g.ObserveDefault(AudioRole.Console,p.Id,time.AddMilliseconds(100)) is null; });
Test("Un choix manuel d’enceintes reste libre pendant le branchement", () => { var g=Make(); g.ObserveDevices([h,s,p],time); return g.ObserveDefault(AudioRole.Console,s.Id,time) is null; });
Test("La dernière sortie volontaire est conservée", () => { var g=Make(); g.ObserveDefault(AudioRole.Console,s.Id,time); g.ObserveDevices([h,s,p],time); return g.ObserveDefault(AudioRole.Console,p.Id,time)?.TargetId==s.Id; });
Test("Un choix valide qui arrive entre les notifications est respecté", () => { var g=Make(); g.ObserveDevices([h,s,p],time); g.ObserveDefault(AudioRole.Console,s.Id,time); return g.ObserveDefault(AudioRole.Console,p.Id,time)?.TargetId==s.Id; });
Test("Un choix manuel de manette hors fenêtre reste libre", () => { var g=Make(); g.ObserveDevices([h,s,p],time); return g.ObserveDefault(AudioRole.Console,p.Id,time.AddSeconds(3)) is null; });
Test("Un choix explicite dans l’application annule la fenêtre", () => { var g=Make(); g.ObserveDevices([h,s,p],time); g.AllowManualChange(); return g.ObserveDefault(AudioRole.Console,p.Id,time) is null; });
Test("La pause ne corrige aucune bascule", () => { var g=Make(); g.SetEnabled(false); g.ObserveDevices([h,s,p],time); return g.ObserveDefault(AudioRole.Console,p.Id,time) is null; });
Test("Mettre en pause annule une arrivée en cours", () => { var g=Make(); g.ObserveDevices([h,s,p],time); g.SetEnabled(false); g.SetEnabled(true); return g.ObserveDefault(AudioRole.Console,p.Id,time) is null; });
Test("Un nouveau branchement réarme la protection", () => { var g=Make(true); g.ObserveDevices([h,s,p with{Active=false}],time); g.ObserveDevices([h,s,p],time.AddSeconds(3)); return g.ObserveDefault(AudioRole.Console,p.Id,time.AddSeconds(3)) is not null; });
Test("Une notification répétée ne réarme pas la protection", () => { var g=Make(); g.ObserveDevices([h,s,p],time); g.ObserveDefault(AudioRole.Console,p.Id,time); g.ObserveDevices([h,s,p],time.AddMilliseconds(400)); return g.ObserveDefault(AudioRole.Console,p.Id,time.AddMilliseconds(500)) is null; });
Test("Un casque retiré n’est jamais choisi comme destination", () => { var g=Make(); g.ObserveDevices([h with{Active=false},s,p],time); return g.ObserveDefault(AudioRole.Console,p.Id,time) is null; });
Test("La sortie de secours disponible est utilisée", () => { var g=Make(); g.FallbackId=s.Id; g.ObserveDevices([h with{Active=false},s,p],time); return g.ObserveDefault(AudioRole.Console,p.Id,time)?.TargetId==s.Id; });
Test("Aucune destination inventée lorsque toutes les sorties manquent", () => { var g=Make(); g.ObserveDevices([p],time); return g.ObserveDefault(AudioRole.Console,p.Id,time) is null; });
Test("Une autre manette n’est pas choisie comme secours", () => { var g=Make(); g.FallbackId=p.Id; g.ObserveDevices([p],time); return g.ObserveDefault(AudioRole.Console,p.Id,time) is null; });
Test("Un appareil inconnu n’est pas bloqué", () => { var g=Make(); return g.ObserveDefault(AudioRole.Console,"other",time) is null; });
Test("L’absence de sortie par défaut est acceptée", () => Make().ObserveDefault(AudioRole.Console,null,time) is null);
Test("Le rôle communications garde son propre choix", () => { var g=Make(); g.ObserveDefault(AudioRole.Communications,s.Id,time); g.ObserveDevices([h,s,p],time); return g.ObserveDefault(AudioRole.Communications,p.Id,time)?.TargetId==s.Id && g.ObserveDefault(AudioRole.Console,p.Id,time)?.TargetId==h.Id; });
Test("Une manette qui disparaît ne peut pas déclencher une restauration", () => { var g=Make(); g.ObserveDevices([h,s,p],time); g.ObserveDevices([h,s],time); return g.ObserveDefault(AudioRole.Console,p.Id,time) is null; });
Test("Les identifiants restent opaques et sont comparés exactement", () => { var g=Make(); g.ObserveDevices([h,s,p],time); return g.ObserveDefault(AudioRole.Console,"PS5",time) is null; });
Test("Les rôles couplés par Windows sont consommés ensemble", () => { var g=Make(); g.ObserveDevices([h,s,p],time); var d=g.ObserveDefault(AudioRole.Console,p.Id,time)!; g.AcknowledgeRestore(d,true); g.AcknowledgeRestore(d with{Role=AudioRole.Multimedia},true); return g.ObserveDefault(AudioRole.Multimedia,p.Id,time) is null; });
Test("Une notification d’un rôle inchangé ne corrige pas un choix existant", () => { var g=Make(); g.ObserveDefault(AudioRole.Communications,p.Id,time); g.ObserveDevices([h,s,p],time); return g.ObserveDefault(AudioRole.Communications,p.Id,time) is null; });
Console.WriteLine($"{results.Count(r=>r.Passed)}/{results.Count} scénarios réussis");
return results.All(r=>r.Passed) ? 0 : 1;
