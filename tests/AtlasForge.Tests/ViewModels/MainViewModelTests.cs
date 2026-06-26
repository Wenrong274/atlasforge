using AtlasForge.ViewModels;

namespace AtlasForge.Tests.ViewModels;

public class MainViewModelTests
{
    [Fact]
    public void DisablingAutoGrid_SeedsGridSizeThatFitsCurrentFrames()
    {
        var vm = new MainViewModel();
        for (var i = 0; i < 29; i++)
        {
            vm.Frames.Add(new FrameItemViewModel { FilePath = $"frame_{i}.png", OrderIndex = i });
        }

        vm.AutoGrid = false;

        Assert.True(vm.GridColumns * vm.GridRows >= vm.Frames.Count);
    }
}
