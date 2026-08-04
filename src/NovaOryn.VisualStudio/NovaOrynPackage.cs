using System;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.VisualStudio;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;
namespace NovaOryn.VisualStudio;
[PackageRegistration(UseManagedResourcesOnly = true, AllowsBackgroundLoading = true)]
[InstalledProductRegistration("NovaOryn OS SDK", "Visual Studio integration for NovaOryn kernels", "0.0.31")]
[ProvideMenuResource("Menus.ctmenu", 1)]
[ProvideAutoLoad(UIContextGuids80.SolutionExists, PackageAutoLoadFlags.BackgroundLoad)]
[Guid(PackageIds.PackageGuidString)]
public sealed class NovaOrynPackage : AsyncPackage
{
    private IVsRegisterPriorityCommandTarget _registration;
    private NovaOrynPriorityLaunchCommandTarget _target;
    private uint _cookie;
    internal NovaOrynOutputPane Output { get; private set; }
    protected override async Task InitializeAsync(CancellationToken token, IProgress<ServiceProgressData> progress)
    {
        await JoinableTaskFactory.SwitchToMainThreadAsync(token);
        Output = await NovaOrynOutputPane.CreateAsync(this, token);
        await NovaOrynCommands.InitializeAsync(this, token);
        _registration = await GetServiceAsync(typeof(SVsRegisterPriorityCommandTarget)) as IVsRegisterPriorityCommandTarget
            ?? throw new InvalidOperationException("Visual Studio priority command service is unavailable.");
        _target = new NovaOrynPriorityLaunchCommandTarget(new NovaOrynLaunchService(this));
        ErrorHandler.ThrowOnFailure(_registration.RegisterPriorityCommandTarget(0, _target, out _cookie));
        Output.WriteLine("[ OK ] NovaOryn Visual Studio extension 0.0.31 loaded.");
    }
    protected override void Dispose(bool disposing)
    {
        ThreadHelper.ThrowIfNotOnUIThread();
        if (disposing && _registration != null && _cookie != 0) _registration.UnregisterPriorityCommandTarget(_cookie);
        base.Dispose(disposing);
    }
}
