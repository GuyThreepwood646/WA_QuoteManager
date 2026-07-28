using Xunit;

// WebApplicationFactory<Program> resolves the entry point via a static HostFactoryResolver
// listener. Two tests each starting their own factory concurrently race on that shared listener
// and intermittently fail with "The entry point exited without ever building an IHost." -
// a WebApplicationFactory/minimal-hosting limitation, not an application bug. Serialising this
// assembly's tests is the documented workaround.
[assembly: CollectionBehavior(DisableTestParallelization = true)]
