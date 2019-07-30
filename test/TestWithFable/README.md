Build the debug version of the type provider.
```
dotnet build ../../src/TypedCssClasses.fsproj -c Debug
```

The `src/App.fsproj` file has a reference to that dll so if you move things around, update the reference.

Restore packages for this project.
```
dotnet restore src/App.fsproj
npm install
```

Try the following activities for testing:
* open this folder in VS Code
* `dotnet build src/App.fsproj`
* `npm run build`

Check `Zanaptak.TypedCssClasses.log.txt` which will exist in this folder or `src/` depending on the build method.
