this_path=$(readlink -f $0)
this_dir=$(dirname $this_path)

/p/valor/ragel-6.10/out/release/ragel -G1 -L -A -e -o $this_dir/AnsiColorFormatter.rl.cs $this_dir/AnsiColorFormatter.rl
#/p/valor/ragel-6.10/out/release/ragel -V -p -o $this_dir/AnsiColorFormatter.rl.dot $this_dir/AnsiColorFormatter.rl
#/p/valor/ragel-6.10/out/release/ragel -x -o $this_dir/AnsiColorFormatter.rl.xml $this_dir/AnsiColorFormatter.rl
