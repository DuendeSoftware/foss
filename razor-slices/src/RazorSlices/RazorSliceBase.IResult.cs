// Copyright (c) Duende Software. All rights reserved.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root for license information.

using System.IO.Pipelines;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;

namespace Duende.RazorSlices;

public abstract partial class RazorSliceBase : IResult, IStatusCodeHttpResult, IContentTypeHttpResult
{
    /// <summary>
    /// Gets or sets the HTTP status code. Defaults to <see cref="StatusCodes.Status200OK"/>
    /// </summary>
    public int StatusCode { get; set; } = StatusCodes.Status200OK;

    int? IStatusCodeHttpResult.StatusCode => StatusCode;

    /// <summary>
    /// Gets or sets the HTTP response content type. Defaults to <c>text/html; charset=utf-8</c>.
    /// </summary>
    public string ContentType { get; set; } = "text/html; charset=utf-8";

    /// <summary>
    /// Gets or sets the encoder used when executing this slice as an HTTP result. If <c>null</c>, the encoder is
    /// resolved from request services, falling back to <see cref="HtmlEncoder.Default"/>.
    /// </summary>
    public HtmlEncoder? HtmlEncoder { get; set; }

    /// <inheritdoc />
    Task IResult.ExecuteAsync(HttpContext httpContext)
    {
        ArgumentNullException.ThrowIfNull(httpContext);

        HtmlEncoder? effectiveHtmlEncoder;
        Task startTask;
        try
        {
            if (this is RazorSlice fullSlice)
            {
                fullSlice.HttpContext = httpContext;
            }

            // Setting HtmlEncoder explicitly avoids resolving request services.
            effectiveHtmlEncoder = HtmlEncoder ?? httpContext.RequestServices.GetService<HtmlEncoder>();
            httpContext.Response.StatusCode = StatusCode;
            httpContext.Response.ContentType = ContentType;

            // Start the response before rendering the template.
            startTask = httpContext.Response.StartAsync(httpContext.RequestAborted);
        }
        catch (Exception error)
        {
            DisposeAfterFailure(error);
            throw;
        }

        if (!startTask.IsCompletedSuccessfully)
        {
            // Go async
            return AwaitStartTaskAndRender(startTask, this, httpContext, effectiveHtmlEncoder);
        }

#pragma warning disable CA2012 // Use ValueTasks correctly: The ValueTask is observed in code below
        var renderTask = RenderAsync(httpContext.Response.BodyWriter, effectiveHtmlEncoder, httpContext.RequestAborted);
#pragma warning restore CA2012

        if (!renderTask.HandleSynchronousCompletion())
        {
            // Go async
            return AwaitRenderTaskAndFlushResponse(renderTask, httpContext.Response.BodyWriter, httpContext.RequestAborted);
        }

        return httpContext.Response.BodyWriter.FlushAsync(httpContext.RequestAborted).GetAsTask();
    }

    private static async Task AwaitStartTaskAndRender(Task startTask, RazorSliceBase slice, HttpContext httpContext, HtmlEncoder? htmlEncoder)
    {
        try
        {
            await startTask.ConfigureAwait(false);
        }
        catch (Exception error)
        {
            slice.DisposeAfterFailure(error);
            throw;
        }
        await slice.RenderAsync(httpContext.Response.BodyWriter, htmlEncoder, httpContext.RequestAborted).ConfigureAwait(false);
        await httpContext.Response.BodyWriter.FlushAsync(httpContext.RequestAborted).ConfigureAwait(false);
    }

    private static async Task AwaitRenderTaskAndFlushResponse(ValueTask renderTask, PipeWriter responseBodyWriter, CancellationToken cancellationToken)
    {
        await renderTask.ConfigureAwait(false);
        await responseBodyWriter.FlushAsync(cancellationToken).ConfigureAwait(false);
    }
}
