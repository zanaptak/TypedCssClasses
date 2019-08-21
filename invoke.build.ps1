# Install PowerShell for your platform:
#   https://docs.microsoft.com/en-us/powershell/scripting/install/installing-powershell
# Install Invoke-Build:
#   https://github.com/nightroman/Invoke-Build
# (Optional) Add "Set-Alias ib Invoke-Build" to your PS profile.
# At a PS prompt, run any build task (optionally use "ib" alias):
#   Invoke-Build build
#   Invoke-Build test
#   Invoke-Build ?  # this lists available tasks

task Clean {
  exec { dotnet clean .\src -c Release }
}

task Build {
  exec { dotnet build .\src -c Release }
}

task CleanTest {
  exec { dotnet clean .\src -c ReleaseTest }
}

task BuildTest {
  exec { dotnet build .\src -c ReleaseTest }
}

task Pack Clean, Build, {
  exec { dotnet pack .\src -c Release }
}

task Test CleanTest, BuildTest, {
  exec { dotnet test .\test\TypedCssClasses.Tests -c ReleaseTest }
}

task . Build
