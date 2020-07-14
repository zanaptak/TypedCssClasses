Build the `Debug` version of the type provider.
```
dotnet build ../../src/TypedCssClasses.fsproj -c Debug
```

The `src/TestWithFable.fsproj` file has a reference to that dll so if you move things around, update the reference.

Restore dependencies for this project.
```
dotnet restore
npm install
```

Install npm sass package globally.
```
npm install -g sass
```

Try the following activities for testing:
* open this folder in VS Code (should have editor completion on `css` types in code files)
* open `App.sln` in VS 2019 (should have editor completion on `css` types in code files)
* `dotnet build` (should build successfully)
* `npm run build` (should build successfully)
* `npm start` (should build, http://localhost:8080/ should display css class names, with blue background from applied css classes)
* check `*.log` files
