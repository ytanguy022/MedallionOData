# MedallionOData

MedallionOData is a lightweight, zero-setup .NET library for creating and querying [OData](http://msdn.microsoft.com/en-us/library/ff478141.aspx) and OData-like services. MedallionOData is available for download as a [NuGet package](https://www.nuget.org/packages/MedallionOData). For a more detailed introduction, check out [my tutorial on Code Ducky](http://www.codeducky.org/introducing-medallionodata/).

## Querying a service

```C#
var context = new ODataQueryContext();
var categories = context.Query(@"http://services.odata.org/v3/odata/odata.svc/Categories");

var foodCategoryId = categories.Where(c => c.Get<string>("Name") == "Food")
    .Select(c => c.Get<int>("ID"))
	// hits http://services.odata.org/v3/odata/odata.svc/Categories?$format=json&$filter=Name eq 'Food'&$select=ID
	.Single();
Console.WriteLine(foodCategoryId); // 0

// we can also perform a more "strongly typed" query using a POCO class
class Category {
	public int ID { get; set; }
	public string Name { get; set; }
}

var categories2 = context.Query<Category>(@"http://services.odata.org/v3/odata/odata.svc/Categories");

var foodCategoryId2 = categories2.Where(c => c.Name == "Food")
	.Select(c => c.ID)
	.Single(); // 0
```

## Creating a service

The example uses EntityFramework and .NET MVC, but the MedallionOData library doesn't depend on either.

```C#
private static readonly ODataService service = new ODataService();

[Route("Categories")] // any form of mapping the route will do
public ActionResult Categories()
{
	using (var db = new MyDbContext())
	{
		IQueryable<Category> query = db.Categories;
		var result = service.Execute(query, HttpUtility.ParseQueryString(this.Request.Url.Query));
		return this.Content(result.Results.ToString(), "application/json");
	}
}
```

## Release notes
- 1.5.0 adds support for .NET Core via .NET Standard 1.5. Dynamic ODataEntity queries are not supported in the .NET Standard build
- 1.4.3 fixes parsing bug for empty OData query parameters
- 1.4.2 optimizes server-side pagination and improves ODataEntity error messages
- 1.4.1 adds missing support for numeric division and negation
- 1.4.0 adds direct support for SQL-based OData services. These services can have fully dynamic schemas, unlike the static typing imposed by providers like EntityFramework
- 1.3.1 fixes a bug where Skip([large #]).Count() could return negative values
- 1.3.0 adds support for fully dynamic services based on in-memory ODataEntity collections
- 1.2.0.0 fixes (enables) support for overriding the client query pipeline. It also adds support for creating queries using relative URIs as well as some improved error messages
- [1.0, 1.2) initial non-semantically-versioned releases

