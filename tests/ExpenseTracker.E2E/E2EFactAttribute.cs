namespace ExpenseTracker.E2E;

/// <summary>A test that needs the browser. It is skipped unless E2E=1, so a plain `dotnet test` (and the CI) stays green.</summary>
public sealed class E2EFactAttribute : FactAttribute
{
    public E2EFactAttribute()
    {
        if (!AppFixture.Enabled)
            Skip = "Pruebas con navegador: defina E2E=1 para ejecutarlas (necesitan Chrome y SQL Server LocalDB).";
    }
}
