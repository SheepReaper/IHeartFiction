using System.Diagnostics.CodeAnalysis;
using System.Security.Cryptography;
using System.Text;

namespace IHFiction.SharedKernel.AgentSkills;

public static class AgentSkillsCatalog
{
    [SuppressMessage("Minor Code Smell", "S1075:URIs should not be hardcoded", Justification = "RFC 0.2.0 well-known schema URI")]
    public const string DiscoverySchemaUri = "https://schemas.agentskills.io/discovery/0.2.0/schema.json";
    public const string ReadContentSkillName = "read-iheartfiction-content";
    public const string ReadContentSkillDescription = "Discover, search, and read published fiction stories and chapters on IHeartFiction using public HTTP/REST and markdown interfaces.";

    [SuppressMessage("Minor Code Smell", "S1075:URIs should not be hardcoded", Justification = "RFC 0.2.0 well-known skill artifact path")]
    public const string ReadContentSkillRelativeUrl = "/.well-known/agent-skills/read-iheartfiction-content/SKILL.md";
    public const string ReadContentSkillType = "skill-md";

    public static string ReadContentSkillMarkdown => """
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

### Anonymous Reading Quick Start (Discovery to Prose)

All public discovery and reading endpoints declare `security: []` in OpenAPI and require **no authentication and no `Authorization` header**:

1. **Discover or Search Published Stories**: `GET https://api.iheartfiction.net/stories?Page=1&PageSize=10` (or discover tag keys via `GET https://api.iheartfiction.net/tags` and filter with `TagKey`).
2. **Inspect Story Structure & Chapters**: `GET https://api.iheartfiction.net/stories/{storyId}` (returns `type`, `tags`, `chapters`, `books`) or `GET https://api.iheartfiction.net/stories/{storyId}/chapters` (returns ordered published chapters; each item's `rel="self"` link points to `/chapters/{chapterId}/content`).
3. **Read Prose Markdown**: `GET https://api.iheartfiction.net/chapters/{chapterId}/content` or `GET https://api.iheartfiction.net/works/{workId}/content` (or fetch `https://iheartfiction.net/read/{workId}` with `Accept: text/markdown`).

Protected authoring and account endpoints (such as `GET /me/profile`, `GET /me/stories`, and `POST /stories`) require OAuth2 / Bearer authentication and return `401 Unauthorized` with `WWW-Authenticate: Bearer resource_metadata="https://api.iheartfiction.net/.well-known/oauth-protected-resource"`.

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

Use `GET https://api.iheartfiction.net/tags` (`q`, `Category`, `Subcategory`, `Sort`, `Fields`, `Page`, `PageSize` 1–200) to discover canonical tag keys (`normalizedKey`), route tokens (`routeKey`), and published `storyCount` values before filtering `GET /stories` with `TagKey`.

### Example: Search Published Stories

```http
GET https://api.iheartfiction.net/stories?q=space&TagKey=genre:sci-fi&Sort=publishedAt%20desc&Page=1&PageSize=10 HTTP/1.1
Accept: application/json
```

### Illustrative Example Response (`LinkedPagedCollection`)

All paginated endpoints (`GET /stories`, `GET /stories/{id}/chapters`, `GET /tags`, `GET /authors`) return a `LinkedPagedCollection` JSON object with `data`, `totalCount`, `currentPage`, `pageSize`, `totalPages`, and hypermedia `links` (`rel="self"`, plus `rel="next-page"` and `rel="previous-page"` when adjacent pages exist):

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
          "href": "https://api.iheartfiction.net/authors/01J8Y6M1A2B3C4D5E6F7G8H9J0",
          "rel": "author",
          "method": "GET"
        },
        {
          "href": "https://api.iheartfiction.net/stories/01J8Y6N0E5K8Z7R1P4M3B2V9X0/chapters",
          "rel": "chapters",
          "method": "GET"
        }
      ]
    }
  ],
  "totalCount": 1,
  "currentPage": 1,
  "pageSize": 10,
  "totalPages": 1,
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

In `GET /stories` list items, published stories expose boolean structure flags indicating how their prose is organized:
- `hasContent: true`: Standalone single-body story with direct body content (exposes `rel="content"` link).
- `hasChapters: true`: Story composed of ordered chapters (exposes `rel="chapters"` link).
- `hasBooks: true`: Story organized into books containing chapters (exposes `rel="chapters"` link).

### Get Story Metadata (`GET /stories/{id}`)

```http
GET https://api.iheartfiction.net/stories/01J8Y6N0E5K8Z7R1P4M3B2V9X0 HTTP/1.1
Accept: application/json
```

Returns detailed story metadata: `id`, `title`, `description`, `completionStatus` (`"InProgress"` or `"Complete"`), `publishedAt`, `isPublished`, `updatedAt`, `createdAt`, `ownerId`, `ownerName`, `type` (`"SingleBody"`, `"MultiChapter"`, or `"MultiBook"`), `hasCoverImage`, `authors` (`[{ id, name }]`), `tags` (`[{ category, subcategory, value, routeKey }]`), `books` (`[{ id, title, description, order, chapters, readCount }]`), `chapters` (`[{ id, title, order, readCount }]`), `readCount`, and `links` (`rel="self"`, `rel="author"`, `rel="meta"`, plus `rel="chapters"` or `rel="content"`). Supports optional `Fields` projection.

### List Published Chapters for a Story (`GET /stories/{id}/chapters`)

```http
GET https://api.iheartfiction.net/stories/01J8Y6N0E5K8Z7R1P4M3B2V9X0/chapters HTTP/1.1
Accept: application/json
```

Returns a `LinkedPagedCollection` (`data`, `totalCount`, `currentPage`, `pageSize`, `totalPages`, `links` including `rel="self"` and `rel="story"`) of published chapters ordered by narrative `order` ascending. Each item contains `chapterId`, `title`, `order`, `publishedAt`, `createdAt`, `updatedAt`, `hasContent`, `readCount`, and per-chapter `links` (`rel="self"`, `rel="content"`, and `rel="story"`, where `self` and `content` point directly to `https://api.iheartfiction.net/chapters/{chapterId}/content`). Supports optional `Fields` projection.

### Resolve Reader Metadata (`GET /works/{id}/meta`)

```http
GET https://api.iheartfiction.net/works/01J8Y6N0E5K8Z7R1P4M3B2V9X0/meta HTTP/1.1
Accept: application/json
```

Returns unified reader metadata for any published work ULID (`id`, `workType`, `readerKind`, `title`, `description`, `publishedAt`, `updatedAt`, `isDirectlyReadable`, `canIndex`, `canonicalUrl`, `pageUrl`, `coverImageUrl`, `coverStoryId`, `storyId`, `storyTitle`, `bookId`, `bookTitle`, `defaultReadableWorkId`, `readableChildren`, `authors`, `readCount`, `links`). For container stories or books, `defaultReadableWorkId` identifies the first directly readable work, and `links` includes `rel="self"`, `rel="story"`, `rel="author"`, and `rel="content"` when `isDirectlyReadable` is `true`.

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
  "workType": "Chapter",
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
    },
    {
      "href": "https://api.iheartfiction.net/works/01J8Y6P3Q4R5S6T7U8V9W0X1Y2/meta",
      "rel": "meta",
      "method": "GET"
    },
    {
      "href": "https://api.iheartfiction.net/stories/01J8Y6N0E5K8Z7R1P4M3B2V9X0",
      "rel": "story",
      "method": "GET"
    },
    {
      "href": "https://api.iheartfiction.net/stories/01J8Y6N0E5K8Z7R1P4M3B2V9X0/chapters",
      "rel": "chapters",
      "method": "GET"
    }
  ]
}
```

> **Note:** On both `GET /works/{id}/content` and `GET /chapters/{id}/content`, `bookId` and `bookTitle` are `null` when a chapter does not belong to a book, and `note1` / `note2` are `null` when absent. On `GET /chapters/{id}/content`, the response fields are `chapterId`, `chapterTitle`, `storyId`, `storyTitle`, `bookId`, `bookTitle`, `contentId`, `content`, `note1`, `note2`, `contentUpdatedAt`, `chapterUpdatedAt`, and `links` (`rel="self"`, `rel="story"`, `rel="chapters"`, plus `rel="previous-chapter"` and `rel="next-chapter"` when adjacent published chapters exist).

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

Returns `userId`, `name`, `avatarUrl` (nullable), `createdAt`, `updatedAt`, `deletedAt` (nullable), `profile` (`{ bio, socialLinks }`), `publishedStories` (`[{ id, title, publishedAt, readCount }]`), `totalStories`, and `links` (`rel="self"`, `rel="stories"`).

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
""";

    public static byte[] GetReadContentSkillBytes() => Encoding.UTF8.GetBytes(ReadContentSkillMarkdown);

    public static string ReadContentSkillDigest =>
        $"sha256:{Convert.ToHexStringLower(SHA256.HashData(GetReadContentSkillBytes()))}";

    public static AgentSkillsDiscoveryDocument CreateDiscoveryDocument() =>
        CreateDiscoveryDocument((Uri?)null);

    public static AgentSkillsDiscoveryDocument CreateDiscoveryDocument(Uri? baseUri) =>
        CreateDiscoveryDocumentInternal(baseUri?.ToString());

    [SuppressMessage("Design", "CA1054:URI-like parameters should not be strings", Justification = "Overload with System.Uri is provided")]
    public static AgentSkillsDiscoveryDocument CreateDiscoveryDocument(string? baseUrl) =>
        CreateDiscoveryDocumentInternal(baseUrl);

    private static AgentSkillsDiscoveryDocument CreateDiscoveryDocumentInternal(string? baseUrl)
    {
        var url = string.IsNullOrWhiteSpace(baseUrl)
            ? ReadContentSkillRelativeUrl
            : $"{baseUrl.TrimEnd('/')}{ReadContentSkillRelativeUrl}";

        return new AgentSkillsDiscoveryDocument(
            DiscoverySchemaUri,
            [
                new AgentSkillEntry(
                    ReadContentSkillName,
                    ReadContentSkillType,
                    ReadContentSkillDescription,
                    url,
                    ReadContentSkillDigest)
            ]);
    }
}
