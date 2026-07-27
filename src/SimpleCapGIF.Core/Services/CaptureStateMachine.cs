using SimpleCapGIF.Core.Models;
using SimpleCapGIF.Localization;

namespace SimpleCapGIF.Core.Services;

public sealed class CaptureStateMachine
{
    public CaptureUiState State { get; private set; } = CaptureUiState.Selecting;
    public void StartCountdown() => Transition(CaptureUiState.Selecting, CaptureUiState.Countdown);

    public void StartRecording()
    {
        if (State is not (CaptureUiState.Selecting or CaptureUiState.Countdown))
        {
            throw new InvalidOperationException(AppStrings.Format(AppStrings.InvalidStateTransitionFormat, State, CaptureUiState.Recording));
        }

        State = CaptureUiState.Recording;
    }

    public void StartEncoding() => Transition(CaptureUiState.Recording, CaptureUiState.Encoding);
    public void Complete() => Transition(CaptureUiState.Encoding, CaptureUiState.Completed);

    public void ReturnToSelecting()
    {
        if (State is not (CaptureUiState.Completed or CaptureUiState.Countdown or CaptureUiState.Recording or CaptureUiState.Encoding))
        {
            throw new InvalidOperationException(AppStrings.Format(AppStrings.InvalidStateReturnFormat, State));
        }

        State = CaptureUiState.Selecting;
    }

    private void Transition(CaptureUiState expected, CaptureUiState next)
    {
        if (State != expected)
        {
            throw new InvalidOperationException(AppStrings.Format(AppStrings.InvalidStateTransitionFormat, State, next));
        }

        State = next;
    }
}
