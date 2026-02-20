Api depends on Application (calls use cases)
Application depends on Domain (uses entities, interfaces)
Infrastructure depends on Application + Domain (implements interfaces defined in Application)
Domain depends on nothing — it is the core, pure C# classes only

Api  →  Application  →  Domain
         ↑
  Infrastructure  →  Domain

  ==============================================================

public ICollection<RefreshToken> RefreshTokens { get; set; } = new List<RefreshToken>();
What is this line in plain terms?

It is just a C# property that holds a list of related objects. Nothing special about it as C# — it is a List<RefreshToken> on the User class. The special part is what EF Core does with it.

Why does EF Core need it?

EF Core looks at your C# classes to figure out the database schema. When it sees:

User has a property: ICollection<RefreshToken> RefreshTokens
RefreshToken has a property: Guid UserId (FK)

EF Core connects the dots and automatically:

creates the foreign key constraint in the DB (RefreshTokens.UserId → Users.UserId)
knows how to JOIN the two tables when you query
Without this property, you would have to write that relationship configuration manually in DbContext. The navigation property is just a shortcut that lets EF Core infer it.

When do you need this kind of property — generic rule:

Add a navigation collection any time the relationship is one-to-many and you might want to:

load the related records together with the parent (Include)
let EF Core figure out the FK constraint automatically
Relationship	Example	Navigation needed
One user → many refresh tokens	User.RefreshTokens	✅
One movie → many ratings	Movie.Ratings	✅
One order → many order items	Order.Items	✅
Two unrelated tables	User and Movie	❌


=======================================================================

Command 1 — Create the migration (generates the migration files):

dotnet ef migrations add InitialCreate --project src/IdentityService/IdentityService.Infrastructure --startup-project src/IdentityService/IdentityService.Api

Command 2 — Apply migration to DB (runs the SQL against Postgres):

dotnet ef database update --project src/IdentityService/IdentityService.Infrastructure --startup-project src/IdentityService/IdentityService.Api