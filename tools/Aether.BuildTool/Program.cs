using Aether.BuildTool;

using var cancellation = new CancellationTokenSource();
Console.CancelKeyPress += (_, args) =>
{
    args.Cancel = true;
    cancellation.Cancel();
};

return await BuildToolApplication.RunAsync(args, cancellation.Token);
