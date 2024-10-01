﻿// Copyright (c) Microsoft. All rights reserved.

using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using OpenAI.Audio;

namespace Microsoft.SemanticKernel.Connectors.OpenAI;

/// <summary>
/// Base class for AI clients that provides common functionality for interacting with OpenAI services.
/// </summary>
internal partial class ClientCore
{
    /// <summary>
    /// Generates an image with the provided configuration.
    /// </summary>
    /// <param name="targetModel">Model identifier</param>
    /// <param name="input">Input audio to generate the text</param>
    /// <param name="executionSettings">Audio-to-text execution settings for the prompt</param>
    /// <param name="cancellationToken">The <see cref="CancellationToken"/> to monitor for cancellation requests. The default is <see cref="CancellationToken.None"/>.</param>
    /// <returns>Url of the generated image</returns>
    internal async Task<IReadOnlyList<TextContent>> GetTextFromAudioContentsAsync(
        string targetModel,
        AudioContent input,
        PromptExecutionSettings? executionSettings,
        CancellationToken cancellationToken)
    {
        if (!input.CanRead)
        {
            throw new ArgumentException("The input audio content is not readable.", nameof(input));
        }

        OpenAIAudioToTextExecutionSettings audioExecutionSettings = OpenAIAudioToTextExecutionSettings.FromExecutionSettings(executionSettings)!;
        AudioTranscriptionOptions? audioOptions = AudioOptionsFromExecutionSettings(audioExecutionSettings);

        Verify.ValidFilename(audioExecutionSettings?.Filename);

        using var memoryStream = new MemoryStream(input.Data!.Value.ToArray());

        AudioTranscription responseData = (await RunRequestAsync(() => this.Client!.GetAudioClient(targetModel).TranscribeAudioAsync(memoryStream, audioExecutionSettings?.Filename, audioOptions)).ConfigureAwait(false)).Value;

        return [new(responseData.Text, targetModel, metadata: GetResponseMetadata(responseData))];
    }

    /// <summary>
    /// Converts <see cref="OpenAIAudioToTextExecutionSettings"/> to <see cref="AudioTranscriptionOptions"/> type.
    /// </summary>
    /// <param name="executionSettings">Instance of <see cref="OpenAIAudioToTextExecutionSettings"/>.</param>
    /// <returns>Instance of <see cref="AudioTranscriptionOptions"/>.</returns>
    private static AudioTranscriptionOptions AudioOptionsFromExecutionSettings(OpenAIAudioToTextExecutionSettings executionSettings)
        => new()
        {
            TimestampGranularities = AudioTimestampGranularities.Default,
            Language = executionSettings.Language,
            Prompt = executionSettings.Prompt,
            Temperature = executionSettings.Temperature,
            ResponseFormat = ConvertResponseFormat(executionSettings.ResponseFormat)
        };

    private static AudioTranscriptionFormat? ConvertResponseFormat(string? responseFormat)
    {
        switch (responseFormat)
        {
            case "json": return AudioTranscriptionFormat.Simple;
            case "verbose_json": return AudioTranscriptionFormat.Verbose;
            case "vtt": return AudioTranscriptionFormat.Vtt;
            case "srt": return AudioTranscriptionFormat.Srt;
            case null: return null;
            default: throw new NotSupportedException($"The audio transcription format '{responseFormat}' is not supported.");
        }
    }

    private static Dictionary<string, object?> GetResponseMetadata(AudioTranscription audioTranscription)
        => new(3)
        {
            [nameof(audioTranscription.Language)] = audioTranscription.Language,
            [nameof(audioTranscription.Duration)] = audioTranscription.Duration,
            [nameof(audioTranscription.Segments)] = audioTranscription.Segments
        };
}
