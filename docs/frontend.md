1. Create web app
   npx create-next-app@latest web-app

2. Page structure

    web-app
        app 
            page.tsx                -> Home(movies list)
            login/page.tsx          -> Login Page
            register/page.tsx       -> Register Page
            components
                Navbar.tsx
                MovieGrid.tsx
                MovieCard.tsx
                RatingStarts.tsx
        lib/
            api.ts                  -> All API calls (identity/Movie/Rating)
        types/
            movie.ts
            user.ts
3. UseState - const[token, setToken] = useState<string | null>(null);
- Use useState when the component has data that can change over time.
What it does
Creates states inside your component
- token -> current value
- setToken -> function to update it
Initial value -> null
You want UI to change based on login state
token value         UI Shown
null                Login/Register
present             Logged In
So this state controls navbar display

4. UseEffect
   useEffect(() => {
    setToken(localStorage.getItem("token"));
   },[]);

    Use useEffect when you need to:

        ✔ Call an API
        ✔ Read from localStorage
        ✔ Run code after render
        ✔ Subscribe to something
   
   It runs after component renders
   Runs this code after component loads
   Localstorage only exisits in browser
   1. We render the component first
   2. Then in UseEffect -> read token from browser storage
   3. Update state -> UI re-renders    