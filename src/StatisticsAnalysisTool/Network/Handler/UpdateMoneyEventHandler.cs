using StatisticsAnalysisTool.Network.Events;
using StatisticsAnalysisTool.Network.Manager;
using System.Threading.Tasks;

namespace StatisticsAnalysisTool.Network.Handler;

public class UpdateMoneyEventHandler : EventPacketHandler<UpdateMoneyEvent>
{
    private readonly TrackingController _trackingController;

    public UpdateMoneyEventHandler(TrackingController trackingController) : base((int) EventCodes.UpdateMoney)
    {
        _trackingController = trackingController;
    }

    protected override async Task OnActionAsync(UpdateMoneyEvent value)
    {
        if (value?.HasCurrentPlayerSilver == true)
        {
            _trackingController.SetTotalPlayerSilver(value.CurrentPlayerSilver.IntegerValue);
        }

        await Task.CompletedTask;
    }
}
