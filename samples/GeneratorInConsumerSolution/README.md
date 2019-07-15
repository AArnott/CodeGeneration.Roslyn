# GeneratorInConsumerSolution

This solution contains two projects: Generator and Consumer.
Generator contains the generator that is used by the Consumer.

The sample generator creates a partial declaration of the
`[GeneratedId]`-annotated type (class/struct) that contains
an additional auto-property:

```csharp
    public System.Guid Id { get; } = System.Guid.NewGuid();
```

### Usage Note

Please note that because [`Directory.Build.props`](./Directory.Build.props) by default uses `*` Version, NuGet will resolve that to the latest *stable* version. Most often is not what you want, so please replace that version with the one you want to test.

Also, if you're validating your development with the sample, please remember that after NuGet once restores a version, it'll cache it in the default `packages` folder - to use new Package with the same version string, you'll need to delete cached packages from that folder.
