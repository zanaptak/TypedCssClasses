# Install PowerShell Core:
#   https://docs.microsoft.com/en-us/powershell/scripting/install/installing-powershell
# Install Invoke-Build:
#   https://github.com/nightroman/Invoke-Build
# (Optional) Add "Set-Alias ib Invoke-Build" to your PS profile.
# At a PS prompt, run any build task (optionally use "ib" alias):
#   Invoke-Build build
#   Invoke-Build ?  # this lists available tasks

param (
    $NuGetApiPushKey = ( property NuGetApiPushKey 'MISSING' ) ,
    $LocalPackageDir = ( property LocalPackageDir 'C:/code/LocalPackages' ) ,
    $Configuration = "Release"
)

[ System.Environment ]::CurrentDirectory = $BuildRoot

$baseProjectName = "TypedCssClasses"
$basePackageName = "Zanaptak.$baseProjectName"
$mainProjectFilePath = "src/$baseProjectName.fsproj"

function trimLeadingZero {
    param ( $item )
    $item = $item.TrimStart( '0' )
    if ( $item -eq "" ) { "0" } else { $item }
}

function combinePrefixSuffix {
    param ( $prefix , $suffix )
    "$prefix-$suffix".TrimEnd( '-' )
}

function writeProjectFileProperty {
    param ( $projectFile , $propertyName , $propertyValue )
    $xml = New-Object System.Xml.XmlDocument
    $xml.PreserveWhitespace = $true
    $xml.Load( $projectFile )

    $nodePath = '/Project/PropertyGroup/' + $propertyName
    $node = $xml.SelectSingleNode( $nodePath )
    $node.InnerText = $propertyValue

    $settings = New-Object System.Xml.XmlWriterSettings
    $settings.OmitXmlDeclaration = $true
    $settings.Encoding = New-Object System.Text.UTF8Encoding( $true )

    $writer = [ System.Xml.XmlWriter ]::Create( $projectFile , $settings )
    try {
        $xml.Save( $writer )
    } finally {
        $writer.Dispose()
    }
}

function readProjectFileProperty {
    param ( $projectFile , $propertyName )
    $nodePath = '/Project/PropertyGroup/' + $propertyName
    $propertyValue =
        Select-Xml -Path $projectFile -XPath $nodePath |
            Select-Object -First 1 |
            & { process { $_.Node.InnerXml.Trim() } }
    $propertyValue
}

task . Build

task Clean {
    exec { dotnet clean ./src -c $Configuration }
}

task Build {
    exec { dotnet build ./src -c $Configuration }
}

task Test {
    $script:Configuration = "ReleaseTest"
} , Clean , Build , {
    exec { dotnet test ./test/TypedCssClasses.Tests -c $Configuration }
}

task IncrementMajor LoadVersion , {
    $version = [ System.Version ] $VersionPrefix
    $newVersionPrefix = [ System.Version ]::new( ( $version.Major + 1 ) , 0 , 0 ).ToString( 3 )
    writeProjectFileProperty $mainProjectFilePath "VersionPrefix" $newVersionPrefix
    writeProjectFileProperty $mainProjectFilePath "VersionSuffix" ""
} , ReportProjectFileVersion

task IncrementMinor LoadVersion , {
    $version = [ System.Version ] $VersionPrefix
    $newVersionPrefix = [ System.Version ]::new( $version.Major , ( $version.Minor + 1 ) , 0 ).ToString( 3 )
    writeProjectFileProperty $mainProjectFilePath "VersionPrefix" $newVersionPrefix
    writeProjectFileProperty $mainProjectFilePath "VersionSuffix" ""
} , ReportProjectFileVersion

task IncrementPatch LoadVersion , {
    $version = [ System.Version ] $VersionPrefix
    $newVersionPrefix = [ System.Version ]::new( $version.Major , $version.Minor , ( $version.Build + 1 ) ).ToString( 3 )
    writeProjectFileProperty $mainProjectFilePath "VersionPrefix" $newVersionPrefix
    writeProjectFileProperty $mainProjectFilePath "VersionSuffix" ""
} , ReportProjectFileVersion

task IncrementPre LoadVersion , {
    $now = Get-Date
    $pre1 = $now.ToString( "yyyyMMdd" )
    $pre2 = trimLeadingZero $now.ToString( "HHmmssff" )
    $newVersionSuffix = "pre.{0}.{1}" -f $pre1 , $pre2
    writeProjectFileProperty $mainProjectFilePath "VersionSuffix" $newVersionSuffix
} , ReportProjectFileVersion

task ClearPre LoadVersion , {
    writeProjectFileProperty $mainProjectFilePath "VersionSuffix" ""
} , ReportProjectFileVersion

task ReportProjectFileVersion {
    $actualVersionPrefix = readProjectFileProperty $mainProjectFilePath "VersionPrefix"
    $actualVersionSuffix = readProjectFileProperty $mainProjectFilePath "VersionSuffix"
    $actualFullVersion = combinePrefixSuffix $actualVersionPrefix $actualVersionSuffix
    Write-Build Green "Version: $actualFullVersion"
}

task LoadVersion {
    $script:VersionPrefix = readProjectFileProperty $mainProjectFilePath "VersionPrefix"
    $script:VersionSuffix = readProjectFileProperty $mainProjectFilePath "VersionSuffix"
    $script:FullVersion = combinePrefixSuffix $VersionPrefix $VersionSuffix
}

task Pack {
    $script:Configuration = "Release"
} , Clean , Build , {
    exec { dotnet pack ./src -c Release }
}

task PackInternal {
    $script:Configuration = "Debug"
} , Clean , Build , LoadVersion , {
    $yearStart = Get-Date -Year ( ( Get-Date ).Year ) -Month 1 -Day 1 -Hour 0 -Minute 0 -Second 0 -Millisecond 0
    $now = Get-Date
    $seconds = [ int ] ( $now - $yearStart ).TotalSeconds
    $internalVersionPrefix = "$VersionPrefix.$seconds"
    exec { dotnet pack ./src -c $Configuration -p:VersionPrefix=$internalVersionPrefix }
    $internalFullVersion = combinePrefixSuffix $internalVersionPrefix $VersionSuffix
    $filename = "$basePackageName.$internalFullVersion.nupkg"
    Copy-Item ./src/bin/$Configuration/$filename $LocalPackageDir
    Write-Build Green "Copied $filename to $LocalPackageDir"
}

task UploadNuGet EnsureCommitted , LoadVersion , {
    if ( $NuGetApiPushKey -eq "MISSING" ) { throw "NuGet key not provided" }
    Set-Location ./src/bin/Release
    $filename = "$basePackageName.$FullVersion.nupkg"
    if ( -not ( Test-Path $filename ) ) { throw "nupkg file not found" }
    $lastHour = ( Get-Date ).AddHours( -1 )
    if ( ( Get-ChildItem $filename ).LastWriteTime -lt $lastHour ) { throw "nupkg file too old" }
    exec { dotnet nuget push $filename -k $NuGetApiPushKey -s https://api.nuget.org/v3/index.json }
}

task EnsureCommitted {
    $gitoutput = exec { git status -s -uall }
    if ( $gitoutput ) { throw "uncommitted changes exist in working directory" }
}
