---
name: read-iheartfiction-content
description: Discover, search, and read published fiction stories and chapters on IHeartFiction using public HTTP/REST and markdown interfaces.
---

# Read IHeartFiction Content

Discover, search, navigate, and read published stories and chapters on IHeartFiction. All reading and discovery operations are public and require no authentication.

## 1. Overview & Service Discovery

IHeartFiction provides two public hostnames:

- **Web Application**: `https://iheartfiction.net` (interactive reader and markdown-negotiated pages)
- **REST API**: `https://api.iheartfiction.net` (structured JSON endpoints, OpenAPI, and content retrieval)

### Discovery Endpoints

- **Agent Skills Discovery Index**: `GET https://iheartfiction.net/.well-known/agent-skills/index.json`
- **RFC 9727 API Catalog**: `GET https://api.iheartfiction.net/.well-known/api-catalog`
- **OpenAPI v1 Specification**: `GET https://api.iheartfiction.net/openapi/v1.json`
- **Interactive API Documentation (Scalar)**: `GET https://api.iheartfiction.net/scalar/v1`
- **OAuth Protected Resource Metadata (RFC 9728)**: `GET https://api.iheartfiction.net/.well-known/oauth-protected-resource`
- **Agent Authentication Guide**: `GET https://iheartfiction.net/auth.md`

Homepage `Link` response headers on `https://iheartfiction.net/` announce machine-readable resources via `rel="api-catalog"`, `rel="service-desc"`, `rel="service-doc"`, and `rel="describedby"`.

---

## 2. Story Discovery and Search

Search and filter published fiction stories using `GET /stories` on `https://api.iheartfiction.net/stories`.

### Query Parameters

| Parameter | Type | Default | Description |
|-----------|------|---------|-------------|
| `q` | string | `""` | Search text matched against story title, summary/description, author names, and tag values. |
| `tag` | string | `""` | Filter by tag slug or name (e.g. `fantasy`, `sci-fi`, `romance`). |
| `sort` | string | `"publishedAt"` | Field to sort by: `publishedAt`, `updatedAt`, `title`, `views`, `likes`, `rating`, `popularity`. |
| `direction` | string | `"desc"` | Sort direction: `asc` or `desc`. |
| `page` | integer | `1` | 1-based page number (must be >= 1). |
| `pageSize` | integer | `20` | Items per page (minimum 1, maximum 50). |
| `fields` | string | `""` | Optional comma-separated data shaping field list (e.g. `id,title,author,summary,rating,wordCount,chapterCount,publishedAt`). |

### Example: Search Stories

```http
GET https://api.iheartfiction.net/stories?q=space&sort=rating&direction=desc&page=1&pageSize=10 HTTP/1.1
Accept: application/json
```

### Example Response

```json
{
  "items": [
    {
      "id": "01J8Y6N0E5K8Z7R1P4M3B2V9X0",
      "title": "Starlight Odyssey",
      "description": "An expedition beyond the solar frontier.",
      "rating": 4.85,
      "wordCount": 42500,
      "chapterCount": 12,
      "publishedAt": "2026-03-15T12:00:00Z",
      "updatedAt": "2026-03-20T18:30:00Z",
      "owner": {
        "id": "01J8Y6M1A2B3C4D5E6F7G8H9J0",
        "name": "Jane Author"
      },
      "tags": [
        { "category": "genre", "subcategory": null, "value": "sci-fi" },
        { "category": "theme", "subcategory": null, "value": "space-exploration" }
      ]
    }
  ],
  "page": 1,
  "pageSize": 10,
  "totalCount": 1,
  "totalPages": 1,
  "hasPreviousPage": false,
  "hasNextPage": false
}
```

---

## 3. Story Details & Chapter Traversal

Stories can be structured as:
- **SingleBody**: Standalone story with direct body content.
- **MultiChapter**: Story composed of numbered chapters.
- **MultiBook**: Story organized into books, each containing chapters.

### Get Story Metadata (`GET /stories/{id}`)

```http
GET https://api.iheartfiction.net/stories/01J8Y6N0E5K8Z7R1P4M3B2V9X0 HTTP/1.1
Accept: application/json
```

Returns detailed story metadata, including `type` (`SingleBody`, `MultiChapter`, `MultiBook`), `ownerId`, `ownerName`, `authors`, `tags`, `chapters`, and `books`.

### List Published Chapters for a Story (`GET /stories/{id}/chapters`)

```http
GET https://api.iheartfiction.net/stories/01J8Y6N0E5K8Z7R1P4M3B2V9X0/chapters HTTP/1.1
Accept: application/json
```

Returns a paged list of published chapters with `chapterId`, `title`, `order`, `publishedAt`, and `readCount`.

---

## 4. Reading Content

Content is stored and delivered in Markdown format.

### Read a Specific Chapter (`GET /chapters/{id}/content`)

```http
GET https://api.iheartfiction.net/chapters/01J8Y6P3Q4R5S6T7U8V9W0X1Y2/content HTTP/1.1
Accept: application/json
```

### Read a Single-Body Story (`GET /stories/{id}/content`)

```http
GET https://api.iheartfiction.net/stories/01J8Y6N0E5K8Z7R1P4M3B2V9X0/content HTTP/1.1
Accept: application/json
```

### Unified Work Content Endpoint (`GET /works/{id}/content`)

Directly readable works (single-body stories or chapters) can also be retrieved by identifier:

```http
GET https://api.iheartfiction.net/works/01J8Y6P3Q4R5S6T7U8V9W0X1Y2/content HTTP/1.1
Accept: application/json
```

### Content Response Structure

```json
{
  "chapterId": "01J8Y6P3Q4R5S6T7U8V9W0X1Y2",
  "chapterTitle": "Chapter 1: The Launch",
  "storyId": "01J8Y6N0E5K8Z7R1P4M3B2V9X0",
  "storyTitle": "Starlight Odyssey",
  "bookId": null,
  "bookTitle": null,
  "contentId": "65e0123456789abcdef01234",
  "content": "# Chapter 1: The Launch\n\nThe engines roared to life as the vessel cleared the orbital dock...",
  "note1": "Author note: Thanks for reading!",
  "note2": null,
  "contentUpdatedAt": "2026-03-15T12:00:00Z",
  "chapterUpdatedAt": "2026-03-15T12:00:00Z"
}
```

### Markdown Content Negotiation via Web Client

When fetching web pages from `https://iheartfiction.net`, agents can request Markdown directly using the `Accept: text/markdown` header:

```http
GET https://iheartfiction.net/read/01J8Y6P3Q4R5S6T7U8V9W0X1Y2 HTTP/1.1
Accept: text/markdown
```

The server returns clean Markdown without HTML boilerplate.

---

## 5. Author Profiles and Works

### Get Author Profile (`GET /authors/{id}`)

```http
GET https://api.iheartfiction.net/authors/01J8Y6M1A2B3C4D5E6F7G8H9J0 HTTP/1.1
Accept: application/json
```

### List Stories by Author (`GET /authors/{id}/stories`)

```http
GET https://api.iheartfiction.net/authors/01J8Y6M1A2B3C4D5E6F7G8H9J0/stories HTTP/1.1
Accept: application/json
```

---

## 6. Authentication Boundary and Read-Only Defaults

1. **Anonymous Public Access**: All story browsing, search, chapter listing, author profile lookups, and content reading endpoints are **public and anonymous**. Do not send `Authorization` headers for normal reading operations.
2. **Optional Delegated Agent Authentication**: For agent tasks acting on behalf of a registered user (e.g. reading private profile preferences or authorized private stories), agents authenticate using Identity Assertion JWT Authorization Grants (ID-JAG) as described in `https://iheartfiction.net/auth.md`.
3. **Strict Read-Only Enforcement**: Agent tokens only receive read scopes (`agent.read`, `profile.read`). Agents cannot create stories, publish chapters, edit metadata, post comments, or perform any mutating action.

---

## 7. Error Handling & Status Codes

| Status Code | Reason | Handling |
|-------------|--------|----------|
| `200 OK` | Request succeeded. | Process payload. |
| `400 Bad Request` | Invalid query parameter (e.g. invalid `sort`, `direction`, `fields`, or `pageSize` > 50). | Check the `domainError` or ProblemDetails response and correct the offending query parameter. |
| `404 Not Found` | Story, chapter, or author does not exist or is not published. | Verify the ULID identifier. |
| `429 Too Many Requests` | Rate limit exceeded. | Read `Retry-After` header and wait before retrying. |
| `500 Internal Server Error` | Transient server error. | Retry with exponential backoff. |
