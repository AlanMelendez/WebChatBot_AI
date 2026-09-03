# RAG Service

This folder contains the first version of the application's Retrieval-Augmented Generation (RAG) feature.

RAG helps an AI answer questions using information from documents. The application does this in two main parts:

1. It prepares the documents and stores their meaning as vectors.
2. It searches those vectors when a user asks a question.

This version is only a demonstration. The documents and the vector database are kept in memory, so the data is lost when the application stops.

## What is RAG?

RAG means **Retrieval-Augmented Generation**.

Without RAG, an AI answers only with the information it already learned or with information included directly in the prompt. With RAG, the application first searches its own documents. It then returns the most useful document parts so they can be used as context for an AI answer.

The basic idea is:

```text
User question
	  |
	  v
Create a vector for the question
	  |
	  v
Search the document vectors
	  |
	  v
Return the most similar document parts
	  |
	  v
Use those parts as context for the AI
```

## Files in this folder

| File | Responsibility |
| --- | --- |
| `DocumentsFromMemoryService.cs` | Creates the sample documents used by the demonstration. |
| `FakeRAGService.cs` | Splits documents, creates embeddings, stores document fragments, and searches for relevant fragments. |
| `IRAGSetvice.cs` | Defines the public RAG service contract through `IRAGService`. |

The data classes used by the RAG service are in the `BlazorAI/DTOs` folder:

| File | Responsibility |
| --- | --- |
| `Document.cs` | Represents a document with a title and content. |
| `VectorDocumentFragment.cs` | Represents one document chunk and its embedding in the vector store. |

## Step 1: Create the source documents

`DocumentsFromMemoryService` returns a list of `Document` objects.

Each document has two values:

- `Title`: the name of the document.
- `Content`: the text inside the document.

The current sample documents are:

- Vacation Policy
- Remote Work
- Equipment Requests
- Technical Support

The documents are written directly in the C# code. This is useful for testing, but a real application would normally read documents from files, a database, a content-management system, or an external storage service.

## Step 2: Request relevant context

The main public method is:

```csharp
FindRelevantContext(string prompt, int topK = 3, CancellationToken cancellationToken = default)
```

Its parameters are:

- `prompt`: the user's question.
- `topK`: the maximum number of matching document fragments to return. The default is `3`.
- `cancellationToken`: allows the operation to stop if the request is cancelled.

The method first calls `Initialize`. This makes sure the document data is ready before the search starts.

## Step 3: Initialize the vector collection only once

The `Initialize` method prepares the vector store.

It follows these steps:

1. Check `_isInitialized`.
2. If it is `true`, return immediately. The documents have already been loaded.
3. If it is `false`, make sure the in-memory collection named `documents` exists.
4. Get the sample documents from `DocumentsFromMemoryService`.
5. Process every document.
6. Set `_isInitialized` to `true` after all documents are stored.

This check prevents the service from creating and storing the same document vectors every time a user asks a question.

## Step 4: Split each document into chunks

Large documents should not be sent as one very large piece. The service divides each document into smaller chunks.

The current code calls:

```csharp
SplitIntoChunks(document.Content, 1000)
```

This means the target maximum size for a combined chunk is 1,000 characters.

The splitter works as follows:

1. Split the document text at each newline character.
2. Remove empty lines.
3. Remove extra spaces at the beginning and end of each line.
4. Treat each remaining line as a paragraph.
5. Keep a chunk in the `current` variable.
6. Read one paragraph at a time.
7. Build a temporary `candidate` by adding the paragraph to `current`.
8. If the candidate fits within the limit, assign it to `current`.
9. If the candidate is too long, save the old `current` chunk in `result`.
10. Set `current` to the paragraph that did not fit. This starts the next chunk and makes sure the paragraph is not lost.
11. After all paragraphs are processed, add the final `current` chunk to `result`.

### Chunking example

Assume the maximum size is `30` characters:

```text
Paragraph 1: Vacation requests need approval.
Paragraph 2: Submit the request early.
```

The service first tries to create:

```text
Vacation requests need approval.
Submit the request early.
```

If that text is longer than 30 characters, the service does this:

```text
result.Add(current);
current = paragraph;
```

The first paragraph is saved, and the second paragraph becomes the beginning of the next chunk. The second paragraph is not thrown away.

### Chunking limitation

The current method combines paragraphs, but it does not split one paragraph into smaller pieces. If one paragraph is already longer than 1,000 characters, that paragraph can create a chunk larger than 1,000 characters.

## Step 5: Create an embedding for every chunk

For every chunk, the service calls the configured `IEmbeddingGenerator`:

```csharp
var vector = await _embeddingGenerator.GenerateVectorAsync(chunk, null, cancellationToken);
```

An embedding is a list of numbers. The numbers represent the meaning of the text.

For example, these sentences have different words but similar meaning:

```text
Employees can work from home three days per week.
Staff may work remotely up to three days each week.
```

Their embeddings should be close to each other because their meanings are similar.

The embedding model used by this vector record is expected to return 1,536 dimensions. The vector store configuration is in `VectorDocumentFragment.cs`.

## Step 6: Create a vector document fragment

After an embedding is created, the service creates a `VectorDocumentFragment`:

- `Id`: a new unique identifier for the fragment.
- `DocumentTitle`: the title of the original document.
- `Text`: the chunk text.
- `Embedding`: the numeric representation of the chunk's meaning.

The fragment is the item that is stored and searched. The original document is not searched as one large value after it has been split.

## Step 7: Save the fragment in the vector store

The service saves each fragment with:

```csharp
await _vectorStore.UpsertAsync(record, cancellationToken);
```

`UpsertAsync` adds the record to the collection or updates it if a record with the same key already exists.

The vector collection is provided by `CommunityToolkit.VectorData.InMemory`. Because it is an in-memory collection:

- It is fast and useful for development.
- It does not require a separate database server.
- Its data disappears when the application stops.
- It is not suitable for production data by itself.

## Step 8: Create an embedding for the user question

After initialization, `FindRelevantContext` creates an embedding for the user's question:

```csharp
var promptEmbedding = await _embeddingGenerator.GenerateVectorAsync(prompt, null, cancellationToken);
```

The question must use the same type of embedding space as the stored document chunks. This lets the vector store compare the question with the chunks correctly.

## Step 9: Search for similar chunks

The service searches the vector collection:

```csharp
await foreach (var document in _vectorStore.SearchAsync(promptEmbedding, topK, null, cancellationToken))
```

The search compares the question vector with the stored document vectors.

`VectorDocumentFragment` configures the comparison to use cosine similarity. Cosine similarity checks how close two vectors are in direction. A higher similarity normally means that the text has a more similar meaning.

The search returns up to `topK` results. With the default value, the service returns up to three relevant fragments.

## Step 10: Format the search results

For every matching fragment, the service creates a text result containing:

```text
Document Title: <original document title>
Text: <matching chunk text>
```

The method returns all formatted results in a `List<string>`.

At this point, the result contains useful context from the documents. The next application step can add that context to an AI prompt so the assistant can answer using the retrieved information.

## Complete execution order

The complete flow for the first search is:

1. A caller sends a question to `FindRelevantContext`.
2. `Initialize` checks whether the vector store is ready.
3. The service creates the `documents` vector collection if needed.
4. The service loads the sample documents.
5. Each document is split into chunks of about 1,000 characters.
6. An embedding is created for each chunk.
7. Each chunk and embedding are saved as a vector document fragment.
8. The service marks initialization as complete.
9. The question is converted into an embedding.
10. The vector store searches for the closest document embeddings.
11. Up to three matching fragments are formatted and returned.
12. A later chat integration can use the returned fragments as AI context.

For later searches in the same service instance, steps 2 through 8 are skipped because `_isInitialized` is already `true`.

## Important current limitations

This is an early implementation, so it has several limitations:

- The documents are hard-coded in `DocumentsFromMemoryService`.
- The vector store is in memory and is not persistent.
- The RAG service must receive a registered embedding generator and vector store through dependency injection before it can run.
- The current staged code defines the RAG service, but it does not yet add the search results to the chat prompt.
- One paragraph longer than the configured limit is not split further.
- There is no dedicated RAG test project yet.
- The file is named `IRAGSetvice.cs`, but the interface inside it is named `IRAGService`.

## Dependencies

The project uses these packages for the RAG work:

- `Microsoft.Extensions.AI`: provides the embedding generator abstraction.
- `CommunityToolkit.VectorData.InMemory`: provides the in-memory vector store.

The embedding model must return vectors with the dimension expected by `VectorDocumentFragment`. The current record is configured for 1,536 dimensions.

## Recommended next steps

1. Register `DocumentsFromMemoryService`, `IRAGService`, `FakeRAGService`, the embedding generator, and the vector store in `Program.cs`.
2. Call `FindRelevantContext` from the chat service.
3. Add the returned fragments to the AI prompt as context.
4. Replace the hard-coded documents with a persistent document source.
5. Replace the in-memory vector store with a persistent vector database for production use.
6. Add tests for document loading, chunking, initialization, and relevant-result search.
7. Rename `IRAGSetvice.cs` to `IRAGService.cs` to match the interface name.
