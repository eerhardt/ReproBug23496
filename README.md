# ReproBug23496
Repro for https://github.com/dotnet/corefx/issues/23496

To repro:

- On a non-Windows machine
- Install latest .NET Core SDK: https://github.com/dotnet/cli#installers-and-binaries
  - ex.
  - `curl -sSL -o dotnet.tar.gz https://dotnetcli.blob.core.windows.net/dotnet/Sdk/master/dotnet-sdk-latest-linux-x64.tar.gz`
  - `sudo mkdir -p /opt/dotnet && sudo tar zxf dotnet.tar.gz -C /opt/dotnet`
- Run the ConsoleApp1 with the installed SDK
  - ex.
  - `ConsoleApp1$ /opt/dotnet/dotnet run`
 
On .NET Core 2.0, the program outputs:
 
```
Hello World
0
```

On .NET Core 2.1, the program outputs:

```
/bin/sh: 1: export LANG=en_US.UTF-8; export LC_ALL=en_US.UTF-8; . /tmp/tmpdfe53bca1c1b44509f2e7608b8d0dc16.exec.cmd: not found
127
```
