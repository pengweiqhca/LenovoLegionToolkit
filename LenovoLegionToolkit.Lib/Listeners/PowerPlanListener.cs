﻿using System;
using System.Linq;
using System.Threading.Tasks;
using LenovoLegionToolkit.Lib.Controllers;
using LenovoLegionToolkit.Lib.Features;
using LenovoLegionToolkit.Lib.Settings;
using LenovoLegionToolkit.Lib.System;
using LenovoLegionToolkit.Lib.Utils;

namespace LenovoLegionToolkit.Lib.Listeners;

public class PowerPlanListener : AbstractEventLogListener
{
    private readonly PowerPlanController _powerPlanController;
    private readonly ApplicationSettings _settings;
    private readonly Vantage _vantage;
    private readonly PowerModeFeature _feature;

    public PowerPlanListener(PowerPlanController powerPlanController, ApplicationSettings settings, Vantage vantage, PowerModeFeature feature)
        : base("System", "*[System[Provider[@Name='Microsoft-Windows-UserModePowerService'] and EventID=12]]")
    {
        _powerPlanController = powerPlanController ?? throw new ArgumentNullException(nameof(powerPlanController)); ;
        _settings = settings ?? throw new ArgumentNullException(nameof(settings));
        _vantage = vantage ?? throw new ArgumentNullException(nameof(vantage));
        _feature = feature ?? throw new ArgumentNullException(nameof(feature));
    }

    protected override async Task OnChangedAsync()
    {
        if (Log.Instance.IsTraceEnabled)
            Log.Instance.Trace($"Power plan changed...");

        if (!await _feature.IsSupportedAsync().ConfigureAwait(false))
        {
            if (Log.Instance.IsTraceEnabled)
                Log.Instance.Trace($"Power modes not supported.");

            return;
        }

        var vantageStatus = await _vantage.GetStatusAsync().ConfigureAwait(false);
        var activateWhenVantageEnabled = _settings.Store.ActivatePowerProfilesWithVantageEnabled;
        if (vantageStatus == SoftwareStatus.Enabled && !activateWhenVantageEnabled)
        {
            if (Log.Instance.IsTraceEnabled)
                Log.Instance.Trace($"Ignoring. [vantage.status={vantageStatus}, activateWhenVantageEnabled={activateWhenVantageEnabled}]");
            return;
        }

        var powerPlans = _powerPlanController.GetPowerPlans().ToArray();
        var activePowerPlan = powerPlans.First(pp => pp.IsActive);

        var powerModes = _powerPlanController.GetMatchingPowerModes(activePowerPlan.Guid);
        if (powerModes.Length != 1)
        {
            if (Log.Instance.IsTraceEnabled)
                Log.Instance.Trace($"Ignoring. [matchingPowerModes={powerModes.Length}]");
            return;
        }

        var powerMode = powerModes[0];
        var currentPowerMode = await _feature.GetStateAsync().ConfigureAwait(false);
        if (powerMode == currentPowerMode)
        {
            if (Log.Instance.IsTraceEnabled)
                Log.Instance.Trace($"Power mode already set.");
            return;
        }

        await _feature.SetStateAsync(powerMode).ConfigureAwait(false);

        if (Log.Instance.IsTraceEnabled)
            Log.Instance.Trace($"Power mode set.");
    }
}