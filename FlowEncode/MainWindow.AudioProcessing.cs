using System;
using System.Threading.Tasks;
using FlowEncode.Domain;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
namespace FlowEncode;

public sealed partial class MainWindow
{
    private void InitializeAudioProcessingInteractions()
    {
        AudioSourcePathTextBox.AddHandler(UIElement.DoubleTappedEvent, new DoubleTappedEventHandler(AudioSourcePathTextBox_DoubleTapped), true);
        AudioOutputPathTextBox.AddHandler(UIElement.DoubleTappedEvent, new DoubleTappedEventHandler(AudioOutputPathTextBox_DoubleTapped), true);
    }

    private void ApplyAudioProcessingLayout(bool stackedWorkspace, bool compactForms)
    {
        AudioProcessingWorkspaceGrid.ColumnSpacing = stackedWorkspace ? 0 : 20;
        AudioProcessingWorkspaceGrid.RowSpacing = stackedWorkspace ? 20 : 0;
        AudioProcessingPrimaryColumn.Width = new GridLength(stackedWorkspace ? 1 : 0.95, GridUnitType.Star);
        AudioProcessingSecondaryColumn.Width = stackedWorkspace
            ? new GridLength(0)
            : new GridLength(1.05, GridUnitType.Star);
        AudioProcessingWorkspacePrimaryRow.Height = GridLength.Auto;
        AudioProcessingWorkspaceSecondaryRow.Height = stackedWorkspace ? GridLength.Auto : new GridLength(0);

        Grid.SetRow(AudioProcessingPrimaryPanel, 0);
        Grid.SetColumn(AudioProcessingPrimaryPanel, 0);
        Grid.SetColumnSpan(AudioProcessingPrimaryPanel, 1);
        Grid.SetRow(AudioProcessingSecondaryPanel, stackedWorkspace ? 1 : 0);
        Grid.SetColumn(AudioProcessingSecondaryPanel, stackedWorkspace ? 0 : 1);
        Grid.SetColumnSpan(AudioProcessingSecondaryPanel, stackedWorkspace ? 2 : 1);

        ConfigureTwoItemGrid(AudioSourcePathGrid, AudioSourcePathActionColumn, AudioSourceBrowseButton, compactForms, GridLength.Auto);
        ConfigureTwoItemGrid(AudioOutputPathGrid, AudioOutputPathActionColumn, AudioOutputBrowseButton, compactForms, GridLength.Auto);
        ConfigureThreeItemGrid(AudioProcessingActionGrid, AudioProcessingCancelColumn, AudioProcessingDeleteColumn, CancelAudioProcessingButton, DeleteAudioProcessingButton, compactForms);
    }

    private async void BrowseAudioSourceButton_Click(object sender, RoutedEventArgs e)
    {
        await PickAudioSourceFileAsync();
    }

    private async void AudioSourcePathTextBox_DoubleTapped(object sender, DoubleTappedRoutedEventArgs e)
    {
        e.Handled = true;
        await PickAudioSourceFileAsync();
    }

    private async Task PickAudioSourceFileAsync()
    {
        var selectedWorkflow = ViewModel.SelectedAudioWorkflow?.Value;
        var preferredPattern = AudioSourceSupport.GetPreferredPickerPattern(selectedWorkflow);
        var filePath = PickFilteredFilePath(
            ViewModel.Texts.SourceHeader,
            ViewModel.AudioProcessingSourcePath,
            ViewModel.Texts.SupportedAudioFileTypeDescription(preferredPattern),
            preferredPattern);

        if (!string.IsNullOrWhiteSpace(filePath))
        {
            await ApplyPickedPathAsync(AudioSourcePathTextBox, filePath, path => ViewModel.AudioProcessingSourcePath = path);
        }
    }

    private async void BrowseAudioOutputButton_Click(object sender, RoutedEventArgs e)
    {
        await PickAudioOutputAsync();
    }

    private async void AudioOutputPathTextBox_DoubleTapped(object sender, DoubleTappedRoutedEventArgs e)
    {
        e.Handled = true;
        await PickAudioOutputAsync();
    }

    private async Task PickAudioOutputAsync()
    {
        var folderPath = await PickFolderPathAsync();
        if (folderPath is not null)
        {
            await ApplyPickedPathAsync(AudioOutputPathTextBox, folderPath, path => ViewModel.AudioProcessingOutputPath = path);
        }
    }

    private async void StartAudioProcessingButton_Click(object sender, RoutedEventArgs e)
    {
        var validationError = ViewModel.ValidateAudioProcessingForStart(out var existingOutputPath);
        if (!string.IsNullOrWhiteSpace(validationError))
        {
            await ShowMessageAsync(ViewModel.Texts.ErrorCannotStartAudioProcessingTitle, validationError);
            return;
        }

        if (!string.IsNullOrWhiteSpace(existingOutputPath))
        {
            var overwriteConfirmed = await ShowConfirmationAsync(
                ViewModel.Texts.OverwriteOutputTitle,
                ViewModel.Texts.OverwriteOutputMessage(existingOutputPath),
                ViewModel.Texts.OverwriteButton,
                ViewModel.Texts.CancelButton,
                ContentDialogButton.Close);

            if (!overwriteConfirmed)
            {
                return;
            }
        }

        var error = await ViewModel.StartAudioProcessingAsync();
        if (!string.IsNullOrWhiteSpace(error))
        {
            await ShowMessageAsync(ViewModel.Texts.ErrorCannotStartAudioProcessingTitle, error);
        }
    }

    private void CancelAudioProcessingButton_Click(object sender, RoutedEventArgs e)
    {
        ViewModel.CancelAudioProcessing();
    }

    private void DeleteAudioProcessingButton_Click(object sender, RoutedEventArgs e)
    {
        ViewModel.ClearAudioProcessingTask();
    }
}
