namespace Bbva.Reconciliation.Application.Sandbox;

/// <summary>Supplies the official BBVA sandbox test plan.</summary>
public interface ISandboxTestPlanRepository
{
    /// <summary>Returns the whole plan. Implementations are expected to cache it.</summary>
    SandboxTestPlan GetPlan();
}
