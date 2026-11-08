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

## 2. Story & Tag Discovery

Search and filter published fiction stories using `GET /stories` on `https://api.iheartfiction.net/stories`.
(`POST /stories` is an authenticated authoring endpoint.)

### Query Parameters (`GET /stories`)

| Parameter | Type | Default | Description |
|-----------|------|---------|-------------|
| `q` | string | `null` | Optional search text (2–100 characters) matched against story title, description, or owner name. |
| `TagKey` | string | `null` | Optional 1–3 comma-separated tag filter tokens (up to 300 characters total) using canonical keys (`category:value` or `category:subcategory:value`), synonyms, unique tag values, or `id:<ulid>`. Stories must match all supplied tags. |
| `AuthorId` | ULID | `null` | Optional author ULID filter returning published stories by that author. |
| `CompletionStatus` | string | `null` | Optional completion status filter: `InProgress` or `Complete`. |
| `Sort` | string | `"publishedAt"` | Comma-separated sort clauses using `publishedAt`, `title`, or `updatedAt`, each optionally followed by `asc` or `desc` (e.g. `publishedAt desc` or `updatedAt desc,title asc`). |
| `Page` | integer | `1` | 1-based page number (must be >= 1). |
| `PageSize` | integer | `50` | Items per page (minimum 1, maximum 100). |
| `Fields` | string | `null` | Optional comma-separated field projection list (max 50 characters). Valid fields: `storyId`, `title`, `description`, `publishedAt`, `updatedAt`, `hasContent`, `hasChapters`, `hasBooks`, `hasCoverImage`, `chapterCount`, `authorId`, `authorName`, `readCount`, `completionStatus`. |

### Discovering Tags (`GET /tags`)

Use `GET https://api.iheartfiction.net/tags` (`q`, `Category`, `Subcategory`, `Sort`, `Fields`, `Page`, `PageSize` 1–200) to discover canonical tag keys and story counts before filtering with `TagKey`.

### Example: Search Published Stories

```http
GET https://api.iheartfiction.net/stories?q=space&TagKey=genre:sci-fi&Sort=publishedAt%20desc&Page=1&PageSize=10 HTTP/1.1
Accept: application/json
```

### Illustrative Example Response (`LinkedPagedCollection`)

```json
{
  "data": [
    {
      "storyId": "01J8Y6N0E5K8Z7R1P4M3B2V9X0",
      "title": "Starlight Odyssey",
      "description": "An expedition beyond the solar frontier.",
      "publishedAt": "2026-03-15T12:00:00Z",
      "updatedAt": "2026-03-20T18:30:00Z",
      "hasContent": false,
      "hasChapters": true,
      "hasBooks": false,
      "hasCoverImage": false,
      "chapterCount": 12,
      "authorId": "01J8Y6M1A2B3C4D5E6F7G8H9J0",
      "authorName": "Jane Author",
      "readCount": 42,
      "completionStatus": "InProgress",
      "links": [
        {
          "href": "https://api.iheartfiction.net/stories/01J8Y6N0E5K8Z7R1P4M3B2V9X0",
          "rel": "self",
          "method": "GET"
        },
        {
          "href": "https://api.iheartfiction.net/stories/01J8Y6N0E5K8Z7R1P4M3B2V9X0/chapters",
          "rel": "list-published-chapters",
          "method": "GET"
        }
      ]
    }
  ],
  "currentPage": 1,
  "pageSize": 10,
  "totalCount": 1,
  "totalPages": 1,
  "hasPreviousPage": false,
  "hasNextPage": false,
  "links": [
    {
      "href": "https://api.iheartfiction.net/stories?q=space&TagKey=genre:sci-fi&Sort=publishedAt%20desc&Page=1&PageSize=10",
      "rel": "self",
      "method": "GET"
    }
  ]
}
```

---

## 3. Story Details, Chapters, & Work Metadata

Published stories expose structure flags indicating how their prose is organized:
- `hasContent: true`: Standalone single-body story with direct body content.
- `hasChapters: true`: Story composed of ordered chapters.
- `hasBooks: true`: Story organized into books containing chapters.

### Get Story Metadata (`GET /stories/{id}`)

```http
GET https://api.iheartfiction.net/stories/01J8Y6N0E5K8Z7R1P4M3B2V9X0 HTTP/1.1
Accept: application/json
```

Returns detailed story metadata, including `storyId`, `title`, `description`, `publishedAt`, `updatedAt`, `workUpdatedAt`, `hasContent`, `hasChapters`, `hasBooks`, `hasCoverImage`, `readCount`, `completionStatus`, `ownerId`, `ownerName`, `authors`, `tags` (`canonicalTags` and `customTags`), `chapters`, `books`, and HATEOAS `links`. Supports optional `Fields` projection.

### List Published Chapters for a Story (`GET /stories/{id}/chapters`)

```http
GET https://api.iheartfiction.net/stories/01J8Y6N0E5K8Z7R1P4M3B2V9X0/chapters HTTP/1.1
Accept: application/json
```

Returns a collection (`data`, `totalCount`, `links`) of published chapters ordered by narrative `order` ascending, with `chapterId`, `title`, `order`, `publishedAt`, `updatedAt`, `contentUpdatedAt`, `readCount`, and per-chapter `links` (including `rel="get-chapter-content"`). Supports optional `Fields` projection.

### Resolve Reader Metadata (`GET /works/{id}/meta`)

```http
GET https://api.iheartfiction.net/works/01J8Y6N0E5K8Z7R1P4M3B2V9X0/meta HTTP/1.1
Accept: application/json
```

Returns unified reader metadata for any published work ULID (`workType`, `readerKind`, `isDirectlyReadable`, `defaultReadableWorkId`, `readableChildren`, `authors`, `readCount`, `links`). For container stories or books, `defaultReadableWorkId` identifies the first directly readable work.

---

## 4. Reading Content

Content is stored and delivered in Markdown format.

### Unified Work Content Endpoint (`GET /works/{id}/content`)

Directly readable works (chapters or single-body stories) can be retrieved via the unified work content endpoint:

```http
GET https://api.iheartfiction.net/works/01J8Y6P3Q4R5S6T7U8V9W0X1Y2/content HTTP/1.1
Accept: application/json
```

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

### Illustrative Content Response (`GET /works/{id}/content`)

```json
{
  "id": "01J8Y6P3Q4R5S6T7U8V9W0X1Y2",
  "workType": "chapter",
  "title": "Chapter 1: The Launch",
  "storyId": "01J8Y6N0E5K8Z7R1P4M3B2V9X0",
  "storyTitle": "Starlight Odyssey",
  "bookId": null,
  "bookTitle": null,
  "contentId": "65e0123456789abcdef01234",
  "content": "# Chapter 1: The Launch\n\nThe engines roared to life as the vessel cleared the orbital dock...",
  "note1": "Author note: Thanks for reading!",
  "note2": null,
  "contentUpdatedAt": "2026-03-15T12:00:00Z",
  "workUpdatedAt": "2026-03-15T12:00:00Z",
  "readCount": 19,
  "links": [
    {
      "href": "https://api.iheartfiction.net/works/01J8Y6P3Q4R5S6T7U8V9W0X1Y2/content",
      "rel": "self",
      "method": "GET"
    }
  ]
}
```

> **Note:** On `GET /works/{id}/content`, `bookId` is `null` when a chapter does not belong to a book. On `GET /chapters/{id}/content`, the response uses `chapterId`, `chapterTitle`, `chapterUpdatedAt`, and a non-nullable `bookId` (`"00000000000000000000000000"` when not in a book).

### Markdown Content Negotiation via Web Client

When fetching web pages from `https://iheartfiction.net` (`/stories/{id}`, `/chapters/{id}`, `/read/{id}`, `/authors/{id}`), agents can request Markdown directly using the `Accept: text/markdown` header:

```http
GET https://iheartfiction.net/read/01J8Y6P3Q4R5S6T7U8V9W0X1Y2 HTTP/1.1
Accept: text/markdown
```

The server returns Markdown (`Content-Type: text/markdown; charset=utf-8` with `Vary: Accept`). Note that `/read/{id}` renders directly for chapters and single-body stories, and redirects (`303 See Other`) to `/stories/{id}` for multi-chapter container stories.

---

## 5. Author Profiles and Works

### List or Search Authors (`GET /authors`)

```http
GET https://api.iheartfiction.net/authors?q=Jane&Sort=name%20asc&Page=1&PageSize=20 HTTP/1.1
Accept: application/json
```

### Get Author Profile (`GET /authors/{id}`)

```http
GET https://api.iheartfiction.net/authors/01J8Y6M1A2B3C4D5E6F7G8H9J0 HTTP/1.1
Accept: application/json
```

Returns `userId`, `name`, `updatedAt`, `bio`, `publishedStories` (the author's published stories embedded directly), and `links`.

### Filter Published Stories by Author (`GET /stories?AuthorId={id}`)

```http
GET https://api.iheartfiction.net/stories?AuthorId=01J8Y6M1A2B3C4D5E6F7G8H9J0 HTTP/1.1
Accept: application/json
```

(On the Web Application, `https://iheartfiction.net/authors/{id}/stories` is an HTML/Markdown page route; the REST API uses `GET /stories?AuthorId={id}` or the `publishedStories` array on `GET /authors/{id}`.)

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
| `400 Bad Request` | Invalid query parameter (e.g. invalid `Sort`, `Fields`, `TagKey`, or `PageSize` > 100). | Check the `domainError` or ProblemDetails response and correct the offending query parameter. |
| `404 Not Found` | Story, chapter, or author does not exist or is not published. | Verify the ULID identifier. |
| `429 Too Many Requests` | Rate limit exceeded. | Read `Retry-After` header and wait before retrying. |
| `500 Internal Server Error` | Transient server error. | Retry with exponential backoff. |