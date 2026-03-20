Identity

curl -X POST http://localhost:5073/auth/login \
  -H "Content-Type: application/json" \
  -d '{"username": "testuser", "password": "Test@1234"}'

  GET Ratings

  curl "http://localhost:5048/rating?movieIds=00000000-0000-0000-0000-000000000001&movieIds=00000000-0000-0000-0000-000000000002"

  POST - add a rating

  curl -X POST http://localhost:5048/rating \
  -H "Content-Type: application/json" \
  -H "Authorization: Bearer YOUR_TOKEN_HERE" \
  -d '{"movieId": "00000000-0000-0000-0000-000000000001", "score": 9, "review": "Great movie!"}'

  