using SkillPilot.Api.Domain;

namespace SkillPilot.Api.Core;

/// <summary>
/// The built-in skill catalog. Resources point to official documentation home pages only,
/// so links stay valid. Topics inside a skill are taught in order.
/// </summary>
public static class Curriculum
{
    public static readonly string[] DefaultPriority =
        { "csharp", "dsa", "oop", "sql", "javascript", "typescript", "angular", "dotnet", "python", "genai" };

    public static readonly List<Skill> Skills = new()
    {
        new() { Key = "csharp", Name = "C# fundamentals", Category = "Core", Order = 1,
            Description = "The language basics you write every day.",
            ResourceName = "Microsoft Learn: C# docs", ResourceUrl = "https://learn.microsoft.com/en-us/dotnet/csharp/" },
        new() { Key = "dsa", Name = "Data structures and algorithms", Category = "Core", Order = 2,
            Description = "Problem solving patterns used in interviews.",
            ResourceName = "NeetCode roadmap", ResourceUrl = "https://neetcode.io/roadmap" },
        new() { Key = "oop", Name = "Object oriented programming", Category = "Core", Order = 3,
            Description = "Design code with classes, interfaces and clear responsibilities.",
            ResourceName = "Microsoft Learn: OOP in C#", ResourceUrl = "https://learn.microsoft.com/en-us/dotnet/csharp/fundamentals/tutorials/oop" },
        new() { Key = "sql", Name = "SQL and databases", Category = "Core", Order = 4,
            Description = "Query and design relational data.",
            ResourceName = "PostgreSQL tutorial", ResourceUrl = "https://www.postgresql.org/docs/current/tutorial.html" },
        new() { Key = "javascript", Name = "JavaScript", Category = "Frontend", Order = 5,
            Description = "The language of the browser.",
            ResourceName = "javascript.info", ResourceUrl = "https://javascript.info/" },
        new() { Key = "typescript", Name = "TypeScript", Category = "Frontend", Order = 6,
            Description = "JavaScript with types, used by Angular.",
            ResourceName = "TypeScript handbook", ResourceUrl = "https://www.typescriptlang.org/docs/handbook/intro.html" },
        new() { Key = "angular", Name = "Angular", Category = "Frontend", Order = 7,
            Description = "Build single page applications.",
            ResourceName = "angular.dev", ResourceUrl = "https://angular.dev/overview" },
        new() { Key = "dotnet", Name = "ASP.NET Core and EF Core", Category = "Backend", Order = 8,
            Description = "Build web APIs that talk to a database.",
            ResourceName = "Microsoft Learn: ASP.NET Core", ResourceUrl = "https://learn.microsoft.com/en-us/aspnet/core/" },
        new() { Key = "python", Name = "Python and FastAPI", Category = "Modern", Order = 9,
            Description = "The main language of AI work.",
            ResourceName = "Python tutorial", ResourceUrl = "https://docs.python.org/3/tutorial/" },
        new() { Key = "genai", Name = "Generative AI engineering", Category = "AI", Order = 10,
            Description = "Use LLMs inside real applications.",
            ResourceName = "Anthropic docs", ResourceUrl = "https://docs.anthropic.com/" },
    };

    private static readonly Dictionary<string, string> Lang = new()
    {
        ["csharp"] = "csharp", ["dsa"] = "csharp", ["oop"] = "csharp", ["sql"] = "sql",
        ["javascript"] = "javascript", ["typescript"] = "typescript", ["angular"] = "typescript",
        ["dotnet"] = "csharp", ["python"] = "python", ["genai"] = "python"
    };

    // (skill, id suffix, title, description, minutes, hasCode)
    private static readonly (string S, string Id, string Title, string Desc, int Min, bool Code)[] Rows =
    {
        ("csharp","types","Variables and types","Value types, reference types, var, and conversions.",60,true),
        ("csharp","flow","Control flow","if, switch, loops and how to choose between them.",60,true),
        ("csharp","methods","Methods","Parameters, return values, overloads and small functions.",60,true),
        ("csharp","strings","Strings","Common string operations and StringBuilder.",60,true),
        ("csharp","collections","Collections","Arrays, List, Dictionary, HashSet and when to use each.",75,true),
        ("csharp","linq","LINQ","Filter, map and group data with LINQ queries.",75,true),
        ("csharp","exceptions","Exceptions","try, catch, finally and throwing your own errors.",45,true),
        ("csharp","async","async and await","Tasks, async methods and why they exist.",75,true),

        ("dsa","complexity","Time and space complexity","Big O thinking: how code slows as input grows.",60,false),
        ("dsa","arrays","Arrays","Indexing, iteration, in place changes and common array problems.",90,true),
        ("dsa","strings","Strings","Character handling, frequency counts and palindromes.",90,true),
        ("dsa","hashmaps","HashMaps and sets","Lookups in constant time, counting and grouping.",90,true),
        ("dsa","two-pointers","Two pointers","Solve sorted array and string problems with two indexes.",90,true),
        ("dsa","sliding-window","Sliding window","Subarray and substring problems with a moving window.",90,true),
        ("dsa","stack-queue","Stack and queue","LIFO and FIFO structures and their classic problems.",90,true),
        ("dsa","linked-lists","Linked lists","Nodes, pointers, reversing and cycle detection.",90,true),
        ("dsa","binary-search","Binary search","Halve the search space, including on answers.",90,true),
        ("dsa","recursion","Recursion","Base case, recursive case and the call stack.",90,true),
        ("dsa","trees","Trees","Binary trees, traversals and recursion on trees.",120,true),
        ("dsa","heaps","Heaps","Priority queues and top K problems.",90,true),
        ("dsa","graphs","Graphs","BFS, DFS and representing graphs.",120,true),
        ("dsa","dp-basics","Dynamic programming basics","Overlapping subproblems and tabulation.",120,true),

        ("oop","classes","Classes and objects","Fields, properties, constructors and instances.",60,true),
        ("oop","encapsulation","Encapsulation","Hide state, expose behaviour.",45,true),
        ("oop","inheritance","Inheritance","Base classes, overriding and when not to inherit.",60,true),
        ("oop","interfaces","Interfaces","Contracts, multiple implementations and why they matter.",60,true),
        ("oop","polymorphism","Polymorphism","One call, many behaviours.",45,true),
        ("oop","solid","SOLID basics","Five habits that keep code easy to change.",60,false),

        ("sql","select","SELECT and filtering","WHERE, ORDER BY, LIMIT and basic expressions.",60,true),
        ("sql","joins","Joins","INNER, LEFT and how rows combine.",75,true),
        ("sql","aggregates","Aggregates and GROUP BY","COUNT, SUM, AVG and HAVING.",60,true),
        ("sql","subqueries","Subqueries and CTEs","Break a query into steps.",75,true),
        ("sql","indexes","Indexes and query plans","Why queries are slow and how to speed them up.",60,false),

        ("javascript","basics","JavaScript basics","let, const, types and operators.",60,true),
        ("javascript","functions","Functions and scope","Closures, arrow functions and this.",75,true),
        ("javascript","arrays-objects","Arrays and objects","map, filter, reduce, destructuring.",75,true),
        ("javascript","async","Promises and async","Callbacks, promises, async and await.",75,true),

        ("typescript","types","Types","Primitives, unions and type inference.",60,true),
        ("typescript","interfaces","Interfaces and types","Describing the shape of data.",60,true),
        ("typescript","generics","Generics","Reusable code that keeps its types.",60,true),

        ("angular","components","Components","Templates, inputs and outputs.",75,true),
        ("angular","templates","Templates and control flow","Binding, if, for and pipes.",60,true),
        ("angular","services","Services and dependency injection","Share logic and data between components.",60,true),
        ("angular","routing","Routing","Pages, params and guards.",60,true),
        ("angular","forms","Forms","Reactive forms and validation.",75,true),
        ("angular","http","HttpClient","Call an API and handle loading and errors.",60,true),

        ("dotnet","efcore","EF Core basics","DbContext, entities, migrations and queries.",90,true),
        ("dotnet","rest","REST APIs","Controllers, routes, status codes and DTOs.",90,true),
        ("dotnet","auth","Authentication basics","Hashing, JWT and protecting endpoints.",75,false),

        ("python","basics","Python basics","Syntax, lists, dicts and functions.",90,true),
        ("python","fastapi","FastAPI","A small API with validation.",90,true),

        ("genai","llm-basics","How LLMs work","Tokens, context windows and limits.",45,false),
        ("genai","prompting","Prompt engineering","Clear instructions, examples and structure.",60,true),
        ("genai","llm-api","Calling an LLM API","Requests, streaming and structured output.",75,true),
        ("genai","rag","RAG basics","Embeddings, retrieval and grounded answers.",90,true),
    };

    public static readonly List<Topic> Topics = Rows
        .GroupBy(r => r.S)
        .SelectMany(g => g.Select((r, i) => new Topic
        {
            Id = $"{r.S}.{r.Id}",
            SkillKey = r.S,
            Title = r.Title,
            Description = r.Desc,
            EstimatedMinutes = r.Min,
            HasCode = r.Code,
            Language = Lang[r.S],
            Order = i + 1
        }))
        .ToList();

    public static Topic? Find(string id) => Topics.FirstOrDefault(t => t.Id == id);
}
