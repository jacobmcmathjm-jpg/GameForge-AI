namespace ForgeCore.Security;

public sealed class WorkspacePathGuard
{
    public string WorkspaceRoot { get; }

    public WorkspacePathGuard(string workspaceRoot)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(workspaceRoot);
        if (IsUncOrDevicePath(workspaceRoot)) throw new ArgumentException("UNC and device paths are not allowed.", nameof(workspaceRoot));
        WorkspaceRoot = Path.TrimEndingDirectorySeparator(Path.GetFullPath(Environment.ExpandEnvironmentVariables(workspaceRoot)));
        Directory.CreateDirectory(WorkspaceRoot);
        RejectReparsePoints(WorkspaceRoot);
    }

    public string Resolve(string candidate, bool allowMissingLeaf = false)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(candidate);
        if (IsUncOrDevicePath(candidate)) throw new UnauthorizedAccessException("UNC and device paths are blocked.");
        var combined = Path.IsPathFullyQualified(candidate) ? candidate : Path.Combine(WorkspaceRoot, candidate);
        var full = Path.GetFullPath(combined);
        var relative = Path.GetRelativePath(WorkspaceRoot, full);
        if (relative == ".." || relative.StartsWith($"..{Path.DirectorySeparatorChar}", StringComparison.Ordinal) || Path.IsPathFullyQualified(relative))
            throw new UnauthorizedAccessException("Path is outside the approved workspace.");

        var existing = File.Exists(full) || Directory.Exists(full) ? full : allowMissingLeaf ? Path.GetDirectoryName(full) : full;
        if (!string.IsNullOrEmpty(existing)) RejectReparsePointsToExistingAncestor(existing);
        return full;
    }

    private static bool IsUncOrDevicePath(string path) => path.StartsWith("\\\\", StringComparison.Ordinal) || path.StartsWith("//", StringComparison.Ordinal);

    private static void RejectReparsePointsToExistingAncestor(string path)
    {
        var current = path;
        while (!string.IsNullOrEmpty(current) && !Directory.Exists(current) && !File.Exists(current)) current = Path.GetDirectoryName(current);
        if (!string.IsNullOrEmpty(current)) RejectReparsePoints(current);
    }

    private static void RejectReparsePoints(string path)
    {
        var root = Path.GetPathRoot(path) ?? throw new InvalidOperationException("Path has no root.");
        var current = root;
        foreach (var segment in path[root.Length..].Split([Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar], StringSplitOptions.RemoveEmptyEntries))
        {
            current = Path.Combine(current, segment);
            if ((File.Exists(current) || Directory.Exists(current)) && (File.GetAttributes(current) & FileAttributes.ReparsePoint) != 0)
                throw new UnauthorizedAccessException("Symbolic links and reparse points are blocked.");
        }
    }
}
