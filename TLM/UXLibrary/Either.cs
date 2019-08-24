namespace UXLibrary {
    public class Either<TLeft, TRight> {
        public Either(TRight right) {
            Right = right;
            IsLeft = false;
        }

        public Either(TLeft left) {
            Left = left;
            IsLeft = true;
        }

        public TLeft Left { get; }

        public TRight Right { get; }

        public bool IsLeft { get; }

        public bool IsRight => !IsLeft;
    }
}