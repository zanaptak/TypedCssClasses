dotnet clean ../src/ -c ReleaseTest
dotnet build ../src/ -c ReleaseTest
dotnet test ./TypedCssClasses.Tests/ -c ReleaseTest
