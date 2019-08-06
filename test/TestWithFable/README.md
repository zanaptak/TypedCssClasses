Build the `DebugLog` version of the type provider.
```
dotnet build ../../src/TypedCssClasses.fsproj -c DebugLog
```

The `src/App.fsproj` file has a reference to that dll so if you move things around, update the reference.

Restore packages for this project.
```
dotnet restore
npm install
```

Try the following activities for testing:
* open this folder in VS Code (should have editor completion on `css.myClass`)
* open `App.sln` in VS 2019 (should have editor completion on `css.myClass`)
* `dotnet build` (should build successfully)
* `npm run build` (should build successfully)
* `npm start` (should build, http://localhost:8080/ should display `css property = myClass`)

Check `Zanaptak.TypedCssClasses.log.txt` which will exist in this folder or `src/` depending on the build method.
