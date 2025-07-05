# Repo Tips

- This repository is a .NET client library for communicating with a Loxone Miniserver.
- The implementation follows the Loxone communication documentation. See:
  - <https://www.loxone.com/wp-content/uploads/datasheets/CommunicatingWithMiniserver.pdf>
  - <https://www.loxone.com/wp-content/uploads/datasheets/StructureFile.pdf>
- To run the unit tests use the .NET SDK (9.0 or later):
  ```sh
  dotnet test LoxNet.Tests/LoxNet.Tests.csproj
  ```
- All project code resides under `LoxNet.Client` and `LoxNet.Tests`.
- C# code uses 4 spaces for indentation and `latest` language version.
