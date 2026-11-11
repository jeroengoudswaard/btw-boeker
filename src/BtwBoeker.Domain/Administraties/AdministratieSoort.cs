namespace BtwBoeker.Domain.Administraties;

/// <summary>
/// De administratie waarvoor BTW-Boeker de boekhouding voert. Het model blijft per administratie (besluit 26), zodat later
/// een tweede object mogelijk is; de app toont er nu één. De waarde wordt als getal opgeslagen.
/// </summary>
public enum AdministratieSoort
{
    Standaard = 1,
}
