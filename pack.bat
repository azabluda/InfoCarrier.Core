for /f "delims=" %%i in ('tools\GitVersion.CommandLine.3.6.5\tools\GitVersion.exe /showvariable NuGetVersion') do set NuGetVersion=%%i

clean ^
  && dotnet restore ^
  && echo DISABLE!!! dotnet test test\InfoCarrier.Core.EFCore.FunctionalTests\InfoCarrier.Core.EFCore.FunctionalTests.csproj ^
  && dotnet pack src\InfoCarrier.Core.EFCore\InfoCarrier.Core.EFCore.csproj --output "..\..\artifacts" --configuration Debug --include-symbols