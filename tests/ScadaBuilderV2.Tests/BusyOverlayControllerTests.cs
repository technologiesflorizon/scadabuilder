using ScadaBuilderV2.App.Shell;

namespace ScadaBuilderV2.Tests;

/// <summary>
/// The counting behind the project loading veil: how many gestures are running, and how many dialogs have
/// asked it to step aside.
/// </summary>
/// <remarks>
/// A boolean would be enough only if gestures never overlapped and no dialog ever interrupted one. Neither
/// holds: an opening gesture raises the conversion consent dialog in its middle, and a failed gesture unwinds
/// while the operator is already clicking again. With a flag, whichever party finishes first clears it, and
/// the window is left either veiled with nothing running - dead - or bare while a project is still loading.
///
/// `BusyOverlayController` carries no WPF reference precisely so this can be tested; the rendering it drives
/// cannot be, and is pinned as source text by `ProjectLoadingOverlayContractTests`.
///
/// Decisions: `DEC-0049`.
/// Contracts: `docs/06_ui_ux/UI_ARCHITECTURE_V2.md` sections 1 and 2.
/// Tests: this file.
/// </remarks>
[TestClass]
public sealed class BusyOverlayControllerTests
{
    [TestMethod]
    public void AGestureRaisesTheVeilAndEndingItLowersTheVeil()
    {
        var controller = new BusyOverlayController(_ => { });

        Assert.IsFalse(controller.State.IsVisible, "nothing is running when the shell opens.");

        controller.BeginBusy("Ouverture de projet", "AMR_REF_SCADA_V2");

        Assert.IsTrue(controller.State.IsVisible);
        Assert.AreEqual("Ouverture de projet", controller.State.Label);
        Assert.AreEqual("AMR_REF_SCADA_V2", controller.State.Detail);

        controller.EndBusy();

        Assert.IsFalse(controller.State.IsVisible);
    }

    [TestMethod]
    public void AGestureThatKnowsNoProjectLeavesTheSecondLineEmptyRatherThanBlank()
    {
        var controller = new BusyOverlayController(_ => { });

        controller.BeginBusy("Ouverture de projet");
        Assert.IsNull(controller.State.Detail, "the file picker does not know the project before the choice.");

        controller.EndBusy();
        controller.BeginBusy("Ouverture de projet", "   ");
        Assert.IsNull(
            controller.State.Detail,
            "whitespace is not a project name; it would open a hole under the label for nothing.");
    }

    [TestMethod]
    public void TheInnerGestureEndingDoesNotLowerTheVeilOnTheOneStillRunning()
    {
        var controller = new BusyOverlayController(_ => { });

        controller.BeginBusy("Ouverture de projet", "AMR_REF_SCADA_V2");
        controller.BeginBusy("Retrait du projet récent", "WIN00008");

        Assert.AreEqual("Retrait du projet récent", controller.State.Label, "the innermost gesture is the one shown.");

        controller.EndBusy();

        Assert.IsTrue(controller.State.IsVisible, "a boolean would have cleared the veil here, mid-load.");
        Assert.AreEqual(
            "Ouverture de projet",
            controller.State.Label,
            "the label of the gesture still running must come back rather than be blanked.");
        Assert.AreEqual("AMR_REF_SCADA_V2", controller.State.Detail);

        controller.EndBusy();

        Assert.IsFalse(controller.State.IsVisible);
    }

    [TestMethod]
    public void ASuspensionTakesTheVeilDownForTheLengthOfTheQuestionAndPutsItBack()
    {
        var controller = new BusyOverlayController(_ => { });
        controller.BeginBusy("Ouverture de projet", "AMR_REF_SCADA_V2");

        using (controller.Suspend())
        {
            Assert.IsFalse(controller.State.IsVisible, "a ring spinning behind a dialog reads as a frozen shell.");
        }

        Assert.IsTrue(controller.State.IsVisible, "the gesture is still running once the question is answered.");
        Assert.AreEqual("Ouverture de projet", controller.State.Label);
    }

    [TestMethod]
    public void NestedSuspensionsOnlyRestoreTheVeilWhenTheLastOneCloses()
    {
        var controller = new BusyOverlayController(_ => { });
        controller.BeginBusy("Ouverture de projet");

        var outer = controller.Suspend();
        var inner = controller.Suspend();

        inner.Dispose();
        Assert.IsFalse(controller.State.IsVisible, "the outer dialog is still on screen.");

        outer.Dispose();
        Assert.IsTrue(controller.State.IsVisible);
    }

    [TestMethod]
    public void DisposingOneSuspensionTwiceDoesNotUnbalanceTheCounter()
    {
        var controller = new BusyOverlayController(_ => { });
        controller.BeginBusy("Ouverture de projet");

        var scope = controller.Suspend();
        scope.Dispose();
        scope.Dispose();

        Assert.AreEqual(0, controller.SuspendDepth);
        Assert.IsTrue(controller.State.IsVisible, "a double dispose must not drive the counter negative.");
    }

    [TestMethod]
    public void SuspendingAnIdleShellNeverRaisesTheVeilOnItsOwn()
    {
        var controller = new BusyOverlayController(_ => { });

        using (controller.Suspend())
        {
            Assert.IsFalse(controller.State.IsVisible);
        }

        Assert.IsFalse(controller.State.IsVisible, "no gesture is running, so there is nothing to restore.");
    }

    [TestMethod]
    public void AnUnmatchedEndNeverDrivesTheCounterNegative()
    {
        var controller = new BusyOverlayController(_ => { });

        controller.EndBusy();
        controller.EndBusy();
        controller.BeginBusy("Ouverture de projet");

        Assert.AreEqual(1, controller.BusyDepth);
        Assert.IsTrue(
            controller.State.IsVisible,
            "a negative counter would swallow the next gesture and show nothing while it loads.");
    }

    [TestMethod]
    public void ARenderThatThrowsWhileSuspendingDoesNotSuppressTheVeilForever()
    {
        var failNextRender = false;
        var controller = new BusyOverlayController(_ =>
        {
            if (failNextRender) throw new InvalidOperationException("the shell refused to render.");
        });

        controller.BeginBusy("Ouverture de projet", "AMR_REF_SCADA_V2");
        Assert.IsTrue(controller.State.IsVisible);

        failNextRender = true;
        Assert.ThrowsException<InvalidOperationException>(() => controller.Suspend());
        failNextRender = false;

        Assert.AreEqual(
            0,
            controller.SuspendDepth,
            "the caller never received the scope, so nothing could ever close this suspension. Left standing, "
            + "it suppresses the veil for the rest of the session and no gesture ever shows anything again.");
        Assert.IsTrue(
            controller.State.IsVisible,
            "the gesture is still running; rolling the suspension back must put the veil back exactly as the "
            + "shell last saw it.");
    }

    [TestMethod]
    public void ARenderThatThrowsWhileRaisingTheVeilDoesNotStrandTheGestureOnTheStack()
    {
        var failNextRender = true;
        var controller = new BusyOverlayController(_ =>
        {
            if (failNextRender) throw new InvalidOperationException("the shell refused to render.");
        });

        Assert.ThrowsException<InvalidOperationException>(() => controller.BeginBusy("Ouverture de projet"));
        failNextRender = false;

        Assert.AreEqual(
            0,
            controller.BusyDepth,
            "the caller's `finally` will not lower a veil it never saw raised, so a gesture left on the stack "
            + "here would veil the window with nothing running.");
        Assert.IsFalse(controller.State.IsVisible);
    }

    [TestMethod]
    public void EveryTransitionIsPushedToTheShell()
    {
        var states = new List<BusyOverlayState>();
        var controller = new BusyOverlayController(states.Add);

        controller.BeginBusy("Ouverture de projet", "AMR_REF_SCADA_V2");
        using (controller.Suspend())
        {
        }

        controller.EndBusy();

        CollectionAssert.AreEqual(
            new[] { true, false, true, false },
            states.Select(state => state.IsVisible).ToArray(),
            "the shell must be told about the raise, the question, the return and the release; a transition "
            + "that is computed but never pushed leaves the window showing the previous state.");
    }
}
