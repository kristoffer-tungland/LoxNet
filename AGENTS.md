# Repo Tips

- This repository is a .NET client library for communicating with a Loxone Miniserver.
- The implementation follows the Loxone communication documentation. See:
  - <https://www.loxone.com/wp-content/uploads/datasheets/CommunicatingWithMiniserver.pdf>
  - <https://www.loxone.com/wp-content/uploads/datasheets/StructureFile.pdf>
  - <https://www.loxone.com/wp-content/uploads/datasheets/OperatingModeSchedule.pdf>
  - <https://www.loxone.com/wp-content/uploads/datasheets/UserManagement.pdf>
- To run the unit tests use the .NET SDK (9.0 or later):
  ```sh
  dotnet test LoxNet.Tests/LoxNet.Tests.csproj
  ```
- All project code resides under `LoxNet.Client` and `LoxNet.Tests`.
- C# code uses 4 spaces for indentation and `latest` language version.
- When parsing JSON into models, use C# records and mark required fields with the `required` keyword.
- Service methods must not return `LoxoneMessage`. Always parse server
  responses with `LoxoneMessageParser` and call `EnsureSuccess()` before
  returning strongly typed results, even when a value is expected.

## Development Notes
- Command execution logic lives in `LoxoneControl` and is reused by derived controls.
- Use `LoxoneControlFactory` to create typed control instances.
- Control types are represented by the `ControlType` enum.
- `LoxoneStructureState` supports a light mode via constructor argument to store only base controls.
- The control factory lives in its own class and is used when mapping controls in the structure cache.
- JSON from `LoxApp3.json` is deserialized into DTO classes decorated with `JsonPropertyName` attributes.
- Command methods on control classes and detail properties include XML documentation.
- Typed controls like `LightControllerV2` and `SwitchControl` expose `Details` objects parsed during factory creation.
- All async methods expose an optional `CancellationToken` parameter and await calls using `ConfigureAwait(false)`.
