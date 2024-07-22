using System;
using System.Collections.Generic;

namespace comp
{
    public static class CompExtensions
    {

        public enum EditType
        {
            none,
            delete,
            insert,
            replace
        };

        public class CompCell<T>
        {
            public CompCell() { }
            public CompCell(EditType type, T a)
            {
                this.type = type;
                this.a = a;
            }
            public CompCell(EditType type, T a, T b)
            {
                this.type = type;
                this.a = a;
                this.b = b;
            }
            public EditType type;
            public T a;
            public T b;
        }
        public static (int, List<CompCell<T>>) Diff<T>(this List<T> expected, List<T> acttual)
            where T : IComparable
        {
            int min(int a, int b, int c) => (a < b) ? ((a < c) ? a : c) : ((b < c) ? b : c);
            int m = expected.Count; // col
            int n = acttual.Count;
            var arr = new int[n + 1, m + 1]; //row, col
            for (var iCol = 0; iCol < m + 1; iCol++) arr[0, iCol] = iCol;
            for (var iRow = 0; iRow < n + 1; iRow++) { arr[iRow, 0] = iRow; }

            for (var iRow = 1; iRow < n + 1; iRow++)
            {
                for (var iCol = 1; iCol < m + 1; iCol++)
                {
                    var (insert, x, delete) = (arr[iRow - 1, iCol], arr[iRow - 1, iCol - 1], arr[iRow, iCol - 1]);
                    if (expected[iCol - 1].CompareTo(acttual[iRow - 1]) == 0)
                    {
                        arr[iRow, iCol] = min(insert + 1, x, delete + 1);
                    }
                    else
                    {
                        arr[iRow, iCol] = min(insert + 1, x + 1, delete + 1);
                    }
                }
            }

            var lst = new List<CompCell<T>>();
            {
                var c = arr[n, m];
                var (iRow, iCol) = (n, m);
                for (; iRow > 0 || iCol > 0;)
                {
                    var s = EditType.replace;
                    if (iRow == 0)
                    {
                        // delete
                        s = EditType.delete;
                    }
                    else if (iCol == 0)
                    {
                        // insert
                        s = EditType.insert;
                    }
                    else
                    {
                        var d = min(arr[iRow - 1, iCol], arr[iRow - 1, iCol - 1], arr[iRow, iCol - 1]);
                        if (c == d)
                        {
                            // equ
                            s = EditType.none;
                        }
                        else
                        {
                            if (arr[iRow - 1, iCol] == d)
                            {
                                // insert
                                s = EditType.insert;
                            }
                            else if (arr[iRow, iCol - 1] == d)
                            {
                                // delete
                                s = EditType.delete;
                            }
                            else
                            {
                                // replace
                                s = EditType.replace;
                            }
                        }
                    }

                    switch (s)
                    {
                        case EditType.none:
                            lst.Add(new CompCell<T>(EditType.none, expected[iCol - 1], acttual[iRow - 1]));
                            iRow--;
                            iCol--;
                            break;
                        case EditType.insert:
                            lst.Add(new CompCell<T>(EditType.insert, acttual[iRow - 1]));
                            iRow--;
                            c--;
                            break;
                        case EditType.delete:
                            lst.Add(new CompCell<T>(EditType.delete, expected[iCol - 1]));
                            iCol--;
                            c--;
                            break;
                        case EditType.replace:
                            lst.Add(new CompCell<T>(EditType.replace, expected[iCol - 1], acttual[iRow - 1]));
                            iRow--;
                            iCol--;
                            c--;
                            break;
                    }
                }
                lst.Reverse();
            }

            return (arr[n, m], lst);
        }

        public static (int, List<CompCell<T>>) LevenshteinDistance<T>(this List<T> expected, List<T> acttual)
            where T : IComparable
        {
            int min(int a, int b, int c) => (a < b) ? ((a < c) ? a : c) : ((b < c) ? b : c);
            int minIndex(int a, int b, int c)
            {
                if (a <= b && a <= c)
                {
                    return 0;
                }
                else if (b <= a && b <= c)
                {
                    return 1;
                }
                else
                {
                    return 2;
                }
            }

            int m = expected.Count; // col
            int n = acttual.Count;
            var arr = new int[n + 1, m + 1]; //row, col
            for (var iCol = 0; iCol <= m; iCol++) arr[0, iCol] = iCol;
            for (var iRow = 1; iRow <= n; iRow++) arr[iRow, 0] = iRow;

            for (var iRow = 1; iRow <= n; iRow++)
            {
                for (var iCol = 1; iCol <= m; iCol++)
                {
                    var d = 1;
                    if (expected[iCol - 1].CompareTo(acttual[iRow - 1]) == 0)
                    {
                        d = 0;
                    }
                    arr[iRow, iCol] = min(arr[iRow, iCol - 1] + 1, arr[iRow - 1, iCol - 1] + d, arr[iRow - 1, iCol] + 1);
                }
            }

            var lst = new List<CompCell<T>>();
            for (var (iRow, iCol, c) = (n, m, arr[n, m]); iRow > 0 || iCol > 0;)
            {
                EditType s;
                var i = iCol == 0 ? 1 :
                    iRow == 0 ? 2 :
                    // insert
                    minIndex(arr[iRow - 1, iCol - 1], arr[iRow - 1, iCol], arr[iRow, iCol - 1]);
                // insert
                if (i == 1)
                    s = EditType.insert;
                // delete
                else if (i == 2)
                    s = EditType.delete;
                // replace or none
                else if (c == arr[iRow - 1, iCol - 1])
                    s = EditType.none;
                else
                    s = EditType.replace;

                switch (s)
                {
                    case EditType.none:
                        lst.Add(new CompCell<T>(EditType.none, expected[iCol - 1], acttual[iRow - 1]));
                        iRow--;
                        iCol--;
                        break;
                    case EditType.insert:
                        lst.Add(new CompCell<T>(EditType.insert, acttual[iRow - 1]));
                        iRow--;
                        c--;
                        break;
                    case EditType.delete:
                        lst.Add(new CompCell<T>(EditType.delete, expected[iCol - 1]));
                        iCol--;
                        c--;
                        break;
                    case EditType.replace:
                        lst.Add(new CompCell<T>(EditType.replace, expected[iCol - 1], acttual[iRow - 1]));
                        iRow--;
                        iCol--;
                        c--;
                        break;
                }
            }
            lst.Reverse();

            return (arr[n, m], lst);
        }
    }
}
