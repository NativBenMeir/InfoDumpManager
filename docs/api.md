# InfoDumpManager API Documentation - Phase 1

## Base URL

- Development: `http://localhost:5000`
- Swagger UI: `http://localhost:5000/swagger`

## Authentication

Phase 1 does not implement authentication. Future phases will include:
- JWT-based authentication
- Claims-based authorization
- Multi-tenancy support

## Endpoints

### Health Check

**GET** `/health`

Check if the API is running and healthy.

**Response:** `200 OK`
```json
{
  "status": "Healthy",
  "timestamp": "2026-01-28T19:00:00Z"
}
```

### Weather Forecast (Sample)

**GET** `/weatherforecast`

Sample endpoint demonstrating API functionality.

**Response:** `200 OK`
```json
[
  {
    "date": "2026-01-29",
    "temperatureC": 15,
    "temperatureF": 59,
    "summary": "Mild"
  },
  {
    "date": "2026-01-30",
    "temperatureC": 22,
    "temperatureF": 71,
    "summary": "Warm"
  }
]
```

**Fields:**
- `date` (string, ISO 8601): The forecast date
- `temperatureC` (integer): Temperature in Celsius
- `temperatureF` (integer): Temperature in Fahrenheit (calculated)
- `summary` (string): Weather description

## Future Endpoints (Planned)

The following endpoints will be implemented in subsequent phases:

### GEM Management

#### Create GEM
**POST** `/api/gems`

Submit a URL for ingestion and processing.

**Request Body:**
```json
{
  "url": "https://example.com/article",
  "title": "Optional title override",
  "notes": "User notes about this GEM"
}
```

**Response:** `201 Created`
```json
{
  "id": "123e4567-e89b-12d3-a456-426614174000",
  "url": "https://example.com/article",
  "status": "Processing",
  "createdAt": "2026-01-28T19:00:00Z"
}
```

#### Get GEM
**GET** `/api/gems/{id}`

Retrieve a specific GEM by ID.

**Response:** `200 OK`
```json
{
  "id": "123e4567-e89b-12d3-a456-426614174000",
  "url": "https://example.com/article",
  "title": "Article Title",
  "summary": "AI-generated summary",
  "categories": ["Technology", "Programming"],
  "tags": ["tutorial", "beginner"],
  "content": "Full extracted content...",
  "status": "Completed",
  "createdAt": "2026-01-28T19:00:00Z",
  "processedAt": "2026-01-28T19:01:30Z"
}
```

#### List GEMs
**GET** `/api/gems`

List all GEMs with pagination and filtering.

**Query Parameters:**
- `page` (integer, default: 1): Page number
- `pageSize` (integer, default: 20, max: 100): Items per page
- `category` (string): Filter by category
- `tag` (string): Filter by tag
- `search` (string): Full-text search query
- `status` (string): Filter by status (Pending, Processing, Completed, Failed)

**Response:** `200 OK`
```json
{
  "items": [
    {
      "id": "123e4567-e89b-12d3-a456-426614174000",
      "url": "https://example.com/article",
      "title": "Article Title",
      "summary": "Brief summary...",
      "categories": ["Technology"],
      "tags": ["tutorial"],
      "createdAt": "2026-01-28T19:00:00Z"
    }
  ],
  "totalCount": 42,
  "page": 1,
  "pageSize": 20,
  "totalPages": 3
}
```

#### Update GEM
**PUT** `/api/gems/{id}`

Update GEM metadata (title, notes, categories, tags).

**Request Body:**
```json
{
  "title": "Updated title",
  "notes": "Updated notes",
  "categoryIds": [1, 2],
  "tagNames": ["tutorial", "advanced"]
}
```

**Response:** `200 OK`

#### Delete GEM
**DELETE** `/api/gems/{id}`

Soft delete a GEM.

**Response:** `204 No Content`

### Category Management

#### List Categories
**GET** `/api/categories`

Get all available categories.

**Response:** `200 OK`
```json
[
  {
    "id": 1,
    "name": "Technology",
    "description": "Articles related to technology",
    "gemCount": 42
  },
  {
    "id": 2,
    "name": "Science",
    "description": "Scientific articles and research",
    "gemCount": 15
  }
]
```

#### Create Category
**POST** `/api/categories`

Create a new category.

**Request Body:**
```json
{
  "name": "New Category",
  "description": "Category description"
}
```

**Response:** `201 Created`

### Tag Management

#### List Tags
**GET** `/api/tags`

Get all tags with usage counts.

**Response:** `200 OK`
```json
[
  {
    "id": 1,
    "name": "tutorial",
    "usageCount": 25
  },
  {
    "id": 2,
    "name": "research",
    "usageCount": 12
  }
]
```

#### Search Tags
**GET** `/api/tags/search`

Search tags by name prefix.

**Query Parameters:**
- `q` (string, required): Search query

**Response:** `200 OK`
```json
[
  {
    "id": 1,
    "name": "tutorial",
    "usageCount": 25
  }
]
```

### Search

#### Full-Text Search
**GET** `/api/search`

Search across all GEMs using full-text search.

**Query Parameters:**
- `q` (string, required): Search query
- `page` (integer, default: 1): Page number
- `pageSize` (integer, default: 20): Items per page
- `categories` (string): Comma-separated category names
- `tags` (string): Comma-separated tag names

**Response:** `200 OK`
```json
{
  "items": [
    {
      "id": "123e4567-e89b-12d3-a456-426614174000",
      "title": "Article Title",
      "summary": "Highlighted summary with <mark>search terms</mark>",
      "relevanceScore": 0.95,
      "url": "https://example.com/article"
    }
  ],
  "totalCount": 5,
  "page": 1,
  "pageSize": 20
}
```

#### Semantic Search
**GET** `/api/search/semantic`

Search using vector similarity (pgvector).

**Query Parameters:**
- `q` (string, required): Search query
- `limit` (integer, default: 10): Maximum results

**Response:** `200 OK`
```json
{
  "items": [
    {
      "id": "123e4567-e89b-12d3-a456-426614174000",
      "title": "Similar Article",
      "summary": "Summary...",
      "similarityScore": 0.92
    }
  ]
}
```

## Error Responses

All endpoints may return the following error responses:

### 400 Bad Request
Invalid request data.

```json
{
  "type": "https://tools.ietf.org/html/rfc7231#section-6.5.1",
  "title": "One or more validation errors occurred.",
  "status": 400,
  "errors": {
    "url": ["The URL field is required."]
  }
}
```

### 404 Not Found
Resource not found.

```json
{
  "type": "https://tools.ietf.org/html/rfc7231#section-6.5.4",
  "title": "Not Found",
  "status": 404,
  "detail": "GEM with ID '123' not found."
}
```

### 500 Internal Server Error
Server error.

```json
{
  "type": "https://tools.ietf.org/html/rfc7231#section-6.6.1",
  "title": "An error occurred while processing your request.",
  "status": 500
}
```

## Rate Limiting

Phase 1 does not implement rate limiting. Future phases will include:
- Per-user rate limits
- Per-API-key rate limits
- Configurable limits per endpoint

## Pagination

List endpoints support pagination with consistent parameters:

- `page`: Page number (1-based, default: 1)
- `pageSize`: Items per page (default: 20, max: 100)

Response includes:
- `items`: Array of results
- `totalCount`: Total number of items
- `page`: Current page
- `pageSize`: Items per page
- `totalPages`: Total number of pages

## Filtering and Sorting

List endpoints support:

**Filtering:**
- By category: `?category=Technology`
- By tag: `?tag=tutorial`
- By status: `?status=Completed`
- By date range: `?from=2026-01-01&to=2026-01-31`

**Sorting:**
- `?sortBy=createdAt&sortOrder=desc`
- `?sortBy=title&sortOrder=asc`

## Content Types

All requests and responses use:
- Content-Type: `application/json`
- Character encoding: UTF-8

## Versioning

API version is specified in the URL path: `/api/v1/...`

Current version: v1

## OpenAPI Specification

The complete OpenAPI 3.0 specification is available at:
- JSON: `http://localhost:5000/swagger/v1/swagger.json`
- Interactive UI: `http://localhost:5000/swagger`

## Testing

### Using Swagger UI

1. Navigate to `http://localhost:5000/swagger`
2. Expand an endpoint
3. Click "Try it out"
4. Fill in parameters
5. Click "Execute"

### Using cURL

```bash
# Get weather forecast
curl http://localhost:5000/weatherforecast

# Future: Create a GEM
curl -X POST http://localhost:5000/api/gems \
  -H "Content-Type: application/json" \
  -d '{
    "url": "https://example.com/article",
    "notes": "Interesting article"
  }'

# Future: Search GEMs
curl "http://localhost:5000/api/search?q=programming&page=1&pageSize=10"
```

### Using HTTPie

```bash
# Get weather forecast
http GET http://localhost:5000/weatherforecast

# Future: Create a GEM
http POST http://localhost:5000/api/gems \
  url="https://example.com/article" \
  notes="Interesting article"
```

## Best Practices

1. **Always validate input** - Use FluentValidation for comprehensive validation
2. **Use pagination** - Don't request all items at once
3. **Handle errors gracefully** - Check status codes and error messages
4. **Use HTTPS in production** - Never send data over plain HTTP
5. **Cache responses** - Use ETags and cache headers when available

## Changelog

### Phase 1 (2026-01-28)
- Initial API structure
- Swagger/OpenAPI documentation
- Sample Weather Forecast endpoint
- Foundation for future endpoints

## Support

For issues, questions, or contributions:
- GitHub Issues: [Repository URL]
- Documentation: [This file]
- Swagger UI: http://localhost:5000/swagger
